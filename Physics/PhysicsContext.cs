using OpenTK.Mathematics;

using Swordfish.ECS;
using Swordfish.Threading;

namespace Swordfish.Physics
{
    public class PhysicsContext
    {
        private int[] bodies;
        private int[] colliders;

        private float accumulator = 0f;

        public readonly ThreadWorker Thread;

        public PhysicsContext()
        {
            bodies = new int[0];
            colliders = new int[0];

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
            ProcessCollisions(deltaTime, stepThru);

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

        public void ProcessCollisions(float deltaTime, bool stepThru = false)
        {
            //  Shorthand
            int entity, other;

            //  Check every entity pair only once
            for (int x = 0; x < colliders.Length; x++)
            {
                entity = colliders[x];

                for (int y = x; y < colliders.Length; ++y)
                {
                    other = colliders[y];
                }
            }
        }
    }
}