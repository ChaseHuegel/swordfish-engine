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
                Simulate(accumulator);
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
                //  Process a physics response for this entity
                ProcessResponse(entity);

                //  Apply velocity and gravity
                Engine.ECS.Do<PositionComponent>(entity, x =>
                {
                    x.position.Y -= 9.8f / Engine.ECS.Get<RigidbodyComponent>(entity).resistance * deltaTime;
                    x.position += Engine.ECS.Get<RigidbodyComponent>(entity).velocity / (Engine.ECS.Get<RigidbodyComponent>(entity).mass/10f) * deltaTime;

                    return x;
                });

                //  Apply drag to velocity
                Engine.ECS.Do<RigidbodyComponent>(entity, x =>
                {
                    float drag = (9.8f + Engine.ECS.Get<RigidbodyComponent>(entity).drag) * deltaTime;

                    x.velocity.X += x.velocity.X < 0 ? drag : -drag;
                    x.velocity.Y += x.velocity.Y < 0 ? drag : -drag;
                    x.velocity.Z += x.velocity.Z < 0 ? drag : -drag;

                    if (x.velocity.LengthFast <= 0.1f)
                        x.velocity = Vector3.Zero;

                    return x;
                });
            }
        }

        private void ProcessResponse(int entity)
        {
            //  Bounce off the floor y=0
            if (Engine.ECS.Get<PositionComponent>(entity).position.Y < 0f)
            {
                //  Clamp y to the floor
                Engine.ECS.Do<PositionComponent>(entity, x =>
                {
                    x.position.Y = 0;

                    return x;
                });

                //  Apply upward velocity = down force to create bounce
                Engine.ECS.Do<RigidbodyComponent>(entity, x =>
                {
                    x.velocity = Vector3.UnitY * 9.8f / Engine.ECS.Get<RigidbodyComponent>(entity).resistance;

                    return x;
                });
            }
        }
    }
}