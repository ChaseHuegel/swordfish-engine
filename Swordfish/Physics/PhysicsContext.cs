using System.Reflection;
using OpenTK.Mathematics;

using Swordfish.ECS;
using Swordfish.Threading;

namespace Swordfish.Physics
{
    public class PhysicsContext
    {
        private int[] entities;

        private float accumulator = 0f;

        public float ThreadTime = 0f;
        private float[] times = new float[6];
        private int timeIndex = 0;
        private float timer = 0f;

        public readonly ThreadWorker Thread;

        public PhysicsContext()
        {
            entities = new int[0];

            Thread = new ThreadWorker(Step, false, "Physics");
        }

        /// <summary>
        /// Push all entities to context that should be acted on by physics
        /// </summary>
        /// <param name="entities"></param>
        internal void Push(int[] entities) => this.entities = entities;

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
                Simulate(accumulator * Engine.Timescale);
                accumulator -= Engine.Settings.Physics.FIXED_TIMESTEP;
            }

            //  TODO: Very quick and dirty stable timing
            timer += deltaTime;
            times[timeIndex] = deltaTime;
            timeIndex++;
            if (timeIndex >= times.Length)
                timeIndex = 0;
            if (timer >= 1f/times.Length)
            {
                timer = 0f;

                float highest = 0f;
                float lowest = 9999f;
                ThreadTime = 0f;
                foreach (float timing in times)
                {
                    ThreadTime += timing;
                    if (timing <= lowest) lowest = timing;
                    if (timing >= highest) highest = timing;
                }

                ThreadTime -= lowest;
                ThreadTime -= highest;
                ThreadTime /= (times.Length - 2);
            }
        }

        public void Simulate(float deltaTime)
        {
            foreach (int entity in entities)
            {
                //  Process a discrete collision response for this entity
                bool hit = ProcessResponse(entity);

                //  Apply rigidbody behavior
                Engine.ECS.Do<RigidbodyComponent>(entity, x =>
                {
                    //  Use surface friction if there is a collision; otherwise use air resistance
                    float friction = (hit ? x.friction : x.resistance);

                    //  Values are inversely proportional and ranged 0-1 for user readability
                    //  We must flip the value and keep in range 0-1 to use for operations
                    //  example:
                    //      value=0 is flipped to 1, there is no friction, gravity is 1:1
                    //      value=1 is flipped to 0, there is complete friction, gravity does not act
                    friction = 1f / (1f - friction);

                    //  Apply gravity if friction is not 0
                    if (friction != 0f) x.velocity.Y -= (1f * deltaTime) / (friction * deltaTime);

                    //  Apply drag to velocity
                    float drag = x.drag * deltaTime;

                    x.velocity.X += x.velocity.X < 0 ? drag : -drag;
                    x.velocity.Y += x.velocity.Y < 0 ? drag : -drag;
                    x.velocity.Z += x.velocity.Z < 0 ? drag : -drag;

                    //  Clamping velocity below a threshold to prevent micro movement
                    if (x.velocity.LengthFast <= 0.1f)
                        x.velocity = Vector3.Zero;

                    return x;
                });

                //  Apply velocity
                Engine.ECS.Do<PositionComponent>(entity, x =>
                {
                    x.position += Engine.ECS.Get<RigidbodyComponent>(entity).velocity * deltaTime;

                    return x;
                });
            }
        }

        private bool ProcessResponse(int entity)
        {
            //  ! Test against a 50 radius sphere at y = -50
            CollisionInfo collision = Phys.FastOverlapSphere(
                Engine.ECS.Get<PositionComponent>(entity).position,
                Engine.ECS.Get<CollisionComponent>(entity).size,
                new Vector3(0, -50, 0),
                50f
            );

            //  If there is a collision...
            if (collision != null)
            {
                //  Step the entity back to the contact point
                Engine.ECS.Do<PositionComponent>(entity, x =>
                {
                    x.position = collision.Contacts[0];

                    return x;
                });

                //  Collision response; bounce off the collision
                Engine.ECS.Do<RigidbodyComponent>(entity, x =>
                {
                    float friction = 1f / (1f - x.friction);
                    float restitution = 1f / (1f - x.restitution);
                    if (friction != 0 && restitution != 0) x.velocity = collision.Normal * x.velocity.Length / friction / restitution;

                    return x;
                });

                //  There was a collision
                return true;
            }

            //  Bounce off the world floor
            if (Engine.ECS.Get<PositionComponent>(entity).position.Y < -300f)
            {
                //  Clamp y to the world floor
                Engine.ECS.Do<PositionComponent>(entity, x =>
                {
                    x.position.Y = -300f;

                    return x;
                });

                //  Collision response; bounce off the collision
                Engine.ECS.Do<RigidbodyComponent>(entity, x =>
                {
                    float friction = 1f / (1f - x.friction);
                    float restitution = 1f / (1f - x.restitution);
                    if (friction != 0 && restitution != 0) x.velocity.Y = -x.velocity.Y / friction / restitution;

                    return x;
                });

                //  There was a collision
                return true;
            }

            //  No collision
            return false;
        }
    }
}