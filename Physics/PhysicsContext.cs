using System.Collections.Generic;

using OpenTK.Mathematics;

using Swordfish.ECS;
using Swordfish.Threading;
using Swordfish.Types;

namespace Swordfish.Physics
{
    public class PhysicsContext
    {
        private int[] bodies;
        private int[] colliders;

        private SphereTree<int> collisionTree;

        private float accumulator = 0f;

        public readonly ThreadWorker Thread;

        public float WorldSize = 0;
        public int ColliderCount = 0;
        public int BroadCollisions = 0;
        public int NarrowCollisions = 0;

        public PhysicsContext()
        {
            bodies = new int[0];
            colliders = new int[0];

            collisionTree = new SphereTree<int>(Vector3.Zero, 1000f, 10f);
            WorldSize = collisionTree.Size;

            Thread = new ThreadWorker(Step, false, "Physics");
        }

        /// <summary>
        /// Push all entities to context that have rigidbodies
        /// </summary>
        /// <param name="entities"></param>
        internal void PushBodies(int[] entities) => this.bodies = entities;

        /// <summary>
        /// Push all entities to context that have collision
        /// </summary>
        /// <param name="entities"></param>
        internal void PushColliders(int[] entities) => this.colliders = entities;

        public void Start()
        {
            Thread.Start();
        }

        public void Shutdown()
        {
            Thread.Stop();
        }

        private void Step(float deltaTime)
        {
            //  Fixed timestep
            accumulator += deltaTime;
            while (accumulator >= Engine.Settings.Physics.FIXED_TIMESTEP)
            {
                Simulate(Engine.Settings.Physics.FIXED_TIMESTEP * Engine.Timescale);
                accumulator -= Engine.Settings.Physics.FIXED_TIMESTEP;
            }
        }

        public void Simulate(float deltaTime, bool stepThru = false)
        {
            //  Handle collisions
            ProcessCollisions();

            foreach (int entity in bodies)
            {
                //  Apply rigidbody behavior
                Engine.ECS.Do<RigidbodyComponent>(entity, x =>
                {
                    //  Apply drag to velocity
                    x.velocity *= 1f - (x.drag * deltaTime);

                    //  Clamp velocity below a threshold to prevent micro movement
                    if (x.velocity.LengthFast <= deltaTime)
                        x.velocity = Vector3.Zero;

                    return x;
                });

                //  Apply velocity
                Engine.ECS.Do<PositionComponent>(entity, x =>
                {
                    x.position += Engine.ECS.Get<RigidbodyComponent>(entity).velocity * deltaTime;
                    x.position.Y -= 9.8f * deltaTime;

                    return x;
                });
            }
        }

        public void ProcessCollisions()
        {
            //  Clear the collision tree
            collisionTree.Clear();

            //  Push every entity with collision to the tree
            for (int x = 0; x < colliders.Length; x++)
                collisionTree.TryAdd(colliders[x], Engine.ECS.Get<PositionComponent>(colliders[x]).position, Engine.ECS.Get<CollisionComponent>(colliders[x]).size);

            //  Broadphase; test the tree with an inaccurate sweep
            List<SphereTreeObjectPair<int>> collisions = new List<SphereTreeObjectPair<int>>();
            collisionTree.SweepForCollisions(collisions);

            //  Narrowphase; accurately test all colliding pairs
            int hits = 0;
            foreach (SphereTreeObjectPair<int> pair in collisions)
            {
                if (Intersection.SphereToSphere(
                    Engine.ECS.Get<PositionComponent>(pair.A).position, Engine.ECS.Get<CollisionComponent>(pair.A).size,
                    Engine.ECS.Get<PositionComponent>(pair.B).position, Engine.ECS.Get<CollisionComponent>(pair.B).size
                ))
                {
                    //  We have a collision
                    hits++;
                }
            }

            ColliderCount = collisionTree.Count;
            BroadCollisions = collisions.Count;
            NarrowCollisions = hits;
        }
    }
}