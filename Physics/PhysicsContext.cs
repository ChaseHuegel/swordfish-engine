using System;
using System.Collections.Generic;

using OpenTK.Mathematics;

using Swordfish.Containers;
using Swordfish.Diagnostics;
using Swordfish.ECS;
using Swordfish.Threading;
using Swordfish.Util;

namespace Swordfish.Physics
{
    public class PhysicsContext
    {
        private int[] bodies;
        private int[] colliders;

        private int[] bodyCache;
        private int[] colliderCache;

        private SphereTree<int> collisionTree;

        private float accumulator = 0f;
        private bool isWarnReady = false;

        public readonly ThreadWorker Thread;

        public float WorldSize = 0;
        public int ColliderCount = 0;
        public int BroadCollisions = 0;
        public int NarrowCollisions = 0;

        public PhysicsContext()
        {
            bodies = new int[0];
            colliders = new int[0];

            collisionTree = new SphereTree<int>(Vector3.Zero, 150f, 10f);
            WorldSize = collisionTree.Size;

            Thread = new ThreadWorker(Step, false, "Physics");
        }

        /// <summary>
        /// Check if a position is within the bounds of the physics world
        /// </summary>
        /// <param name="pos">the position to check</param>
        /// <returns>true if position is within physics boundries; otherwise false</returns>
        public bool InBounds(Vector3 pos)
        {
            return Math.Abs(pos.X) <= Engine.Physics.WorldSize && Math.Abs(pos.Y) <= Engine.Physics.WorldSize && Math.Abs(pos.Z) <= Engine.Physics.WorldSize;
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

        internal void Step(float deltaTime)
        {
            //  Accumulate fixed timestep
            accumulator += deltaTime;

            //  Warnings are ready ~1/second
            if (!isWarnReady)
                isWarnReady = (Engine.Time >= 0.9f);

            // Prevent and warn hanging up
            if (accumulator > Engine.Settings.Physics.MAX_TIMESTEP)
            {
                //  Only send warnings at set interval to prevent spam
                if (isWarnReady)
                {
                    Debug.Log("Physics simulation took too long!", LogType.WARNING, true);
                    isWarnReady = false;
                }

                //  Accumulate timesteps determines how the simulation behaves when lagging
                //  This will allow physics to run behind for a smooth, accurate experience OR stay current to the state at the cost of lagging
                //      DO USE when you need results that are smooth and accurate
                //          i.e. simulations, high-speed physics, replays
                //      DO NOT use when you need results that are consistent and current
                //          i.e. networked physics
                //
                //  Generally it is advised to use accumulation except when networking physics that are key to gameplay
                //      The server performing physics simulation should run with accumulation for accuracy, while
                //      clients should disable so their simulation is consistent with the current state
                //      If physics is not key to your use, i.e. used for visuals, then it is recommended to use accumulation
                //
                //  Enabling accumulation will allow the simulation to run behind and accumulate timesteps
                //      This causes the simulation to run behind at it's own pace instead of lagging when overloaded
                //      Because of this, it will appear to be a smooth and lag-free simulation for the user
                //      This maintains high accuracy at the cost of performance-dependent playback (NOT performance-dependent results)
                //  Disabling will skip timesteps to allow the simulation to stay current
                //      This is because it will begin skipping timesteps until it is no longer lagging, preventing overloading
                //      This will result in visible lag to the user as they will see physics objects skipping updates
                //      However this can cause a loss in accuracy as in-between steps are lost which MAY cause performance-dependent results
                if (!Engine.Settings.Physics.ACCUMULATE_TIMESTEPS)
                {
                    accumulator -= Engine.Settings.Physics.FIXED_TIMESTEP;
                    return;
                }
            }

            //  Step through # of times based on delta time to ensure it is fixed
            while (accumulator >= Engine.Settings.Physics.FIXED_TIMESTEP)
            {
                Simulate(Engine.Settings.Physics.FIXED_TIMESTEP * Engine.Timescale);
                accumulator -= Engine.Settings.Physics.FIXED_TIMESTEP;
            }
        }

        internal void Simulate(float deltaTime, bool stepThru = false)
        {
            //  ! Cache the entities
            //  ! Excessive accessing of the arrays at the same time they
            //  ! are being pushed can cause infinite loops due to the threading
            //  TODO Locking the arrays for thread safety is slow for how often they are accessed
            //  TODO Caching works well but having to remember to do it is accident-prone
            bodyCache = bodies;
            colliderCache = colliders;

            ProcessCollisions();

            foreach (int entity in bodyCache)
            {
                //  Apply rigidbody behavior
                Engine.ECS.Do<RigidbodyComponent>(entity, x =>
                {
                    //  Apply drag to velocity at a rate of m/s
                    x.velocity *= 1f - (x.drag * deltaTime);

                    //  Decay impulse so that it takes place over the period of 1s
                    x.impulse *= 1f - deltaTime;

                    //  Clamp velocity below a threshold to prevent micro movement
                    if (x.velocity.LengthFast <= deltaTime)
                        x.velocity = Vector3.Zero;

                    return x;
                });

                //  Apply velocity
                Engine.ECS.Do<TransformComponent>(entity, x =>
                {
                    x.Translate(deltaTime *
                        (
                            Engine.ECS.Get<RigidbodyComponent>(entity).velocity
                            + Engine.ECS.Get<RigidbodyComponent>(entity).acceleration
                            + Engine.ECS.Get<RigidbodyComponent>(entity).impulse
                        )
                    );

                    //  Gravity
                    //  TODO gravity should be defined either by context or rigidbody not hardcoded
                    x.Translate(0f, -9.8f * deltaTime * (1f - Engine.ECS.Get<RigidbodyComponent>(entity).resistance), 0f);

                    return x;
                });
            }
        }

        //  TODO tidy up and break into pieces, this is a dirty proof of concept
        internal void ProcessCollisions()
        {
            //  Clear the collision tree
            collisionTree.Clear();

            //  Push every entity with collision to the tree
            foreach (int collider in colliderCache)
                collisionTree.TryAdd(collider, Engine.ECS.Get<TransformComponent>(collider).position, Engine.ECS.Get<CollisionComponent>(collider).size);

            //  Broadphase; test every object against the tree
            //  TODO should only test objects which have moved
            List<SphereTreeObjectPair<int>> collisions = new List<SphereTreeObjectPair<int>>();
            foreach (int collider in colliderCache)
            {
                List<int> hits = new List<int>();
                collisionTree.GetColliding(Engine.ECS.Get<TransformComponent>(collider).position, Engine.ECS.Get<CollisionComponent>(collider).size, hits);

                //  Create collision pairings from all hit objects
                // TODO check for shared parents
                // TODO check for duplicate pairs
                foreach (int other in hits) if (collider != other)
                        collisions.Add(new SphereTreeObjectPair<int>() { A = collider, B = other });
            }

            List<int> hitEntities = new List<int>();
            List<int> broadHits = new List<int>();

            //  Narrowphase; accurately test all colliding pairs
            foreach (SphereTreeObjectPair<int> pair in collisions)
            {
                broadHits.Add(pair.A);
                broadHits.Add(pair.B);

                if (Intersection.SphereToSphere(
                    Engine.ECS.Get<TransformComponent>(pair.A).position, Engine.ECS.Get<CollisionComponent>(pair.A).size,
                    Engine.ECS.Get<TransformComponent>(pair.B).position, Engine.ECS.Get<CollisionComponent>(pair.B).size
                ))
                {
                    //  We have a collision
                    hitEntities.Add(pair.A);
                    hitEntities.Add(pair.B);

                    Vector3 relativeVector = Engine.ECS.Get<TransformComponent>(pair.A).position - Engine.ECS.Get<TransformComponent>(pair.B).position;

                    //  Get penetration depth
                    float depth = Vector3.Dot(relativeVector, relativeVector) - (Engine.ECS.Get<CollisionComponent>(pair.A).size + Engine.ECS.Get<CollisionComponent>(pair.B).size);
                    depth = Math.Abs(depth);

                    //  The collision normal
                    Vector3 normal = relativeVector.Normalized();

                    //  Propagate response up parent-child chain
                    int parent;

                    int target = pair.A;
                    float targetMass = Engine.ECS.Get<RigidbodyComponent>(target).mass;
                    Vector3 targetVelocity = Engine.ECS.Get<RigidbodyComponent>(target).velocity;

                    int other = pair.B;
                    float otherMass = Engine.ECS.Get<RigidbodyComponent>(other).mass;
                    Vector3 otherVelocity = Engine.ECS.Get<RigidbodyComponent>(other).velocity;

                    while ((parent = Engine.ECS.Get<TransformComponent>(target).parent) != Entity.Null)
                    {
                        target = parent;
                        targetMass += Engine.ECS.Get<RigidbodyComponent>(target).mass;
                        targetVelocity += Engine.ECS.Get<RigidbodyComponent>(target).velocity;
                    }

                    while ((parent = Engine.ECS.Get<TransformComponent>(other).parent) != Entity.Null)
                    {
                        other = parent;
                        otherMass += Engine.ECS.Get<RigidbodyComponent>(other).mass;
                        otherVelocity += Engine.ECS.Get<RigidbodyComponent>(other).velocity;
                    }

                    //  Ignore response between objects sharing a parent
                    if (target == other)
                        continue;

                    //  TODO skin should be defined by the rigidbody
                    float colliderSkin = 0.01f;
                    float solverModifier = 0.5f;
                    float energy = (targetVelocity.Length - otherVelocity.Length) * normal.Length / (normal.Length * normal.Length * (1f / targetMass + 1f / otherMass));

                    // ******  Physics solver ****** //
                    Engine.ECS.Do<RigidbodyComponent>(target, x =>
                    {
                        float response = x.restitution * energy;

                        x.velocity += targetVelocity - response * normal / targetMass;
                        return x;
                    });

                    Engine.ECS.Do<RigidbodyComponent>(other, x =>
                    {
                        float response = x.restitution * energy;

                        x.velocity += targetVelocity + response * normal / otherMass;
                        return x;
                    });

                    // ******  Position solver ****** //
                    Engine.ECS.Do<TransformComponent>(target, x =>
                    {
                        x.position += normal * (depth + colliderSkin) * solverModifier * (2f*Engine.ECS.Get<RigidbodyComponent>(target).restitution);
                        return x;
                    });

                    Engine.ECS.Do<TransformComponent>(other, x =>
                    {
                        x.position += -normal * (depth + colliderSkin) * solverModifier * (2f*Engine.ECS.Get<RigidbodyComponent>(other).restitution);
                        return x;
                    });
                }
            }

            //  Update collision flags for debugging
            foreach (int entity in colliderCache)
                Engine.ECS.Do<CollisionComponent>(entity, x =>
                {
                    x.colliding = hitEntities.Contains(entity);
                    x.broadHit = broadHits.Contains(entity);
                    return x;
                });

            //  Counters for debugging
            ColliderCount = collisionTree.Count;
            BroadCollisions = collisions.Count;
            NarrowCollisions = hitEntities.Count / 2;
        }
    }
}