using System.Reflection.Metadata;
using OpenTK.Mathematics;
using Swordfish.Diagnostics;
using Swordfish.ECS;
using Swordfish.Threading;
using Swordfish.Util;

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
                Engine.ECS.Do<PositionComponent>(entity, x =>
                {
                    x.position -= 9.8f * Vector3.UnitY * deltaTime * Engine.ECS.Get<RigidbodyComponent>(entity).mass;

                    return x;
                });

                if (Engine.ECS.Get<PositionComponent>(entity).position.Y < 0)
                    Engine.ECS.DestroyEntity(entity);
            }
        }
    }
}