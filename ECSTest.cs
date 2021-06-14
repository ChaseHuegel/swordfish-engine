using System;
using System.Globalization;
using System.Text;
using System.Threading;
using OpenTK.Mathematics;
using Swordfish;
using Swordfish.ECS;
using Swordfish.Rendering;

namespace ECSTest
{
    [ComponentSystem(typeof(PositionComponent))]
    public class GravitySystem : ComponentSystem
    {
        public override void OnUpdate(float deltaTime)
        {
            foreach (Entity entity in entities)
            {
                Engine.ECS.Do<PositionComponent>(entity, x =>
                {
                    x.position -= 9.8f * Vector3.UnitY * deltaTime;

                    return x;
                });

                if (Engine.ECS.Get<PositionComponent>(entity).position.Y <= 0)
                    Engine.ECS.DestroyEntity(entity);
            }
        }
    }

    [ComponentSystem(typeof(RotationComponent))]
    public class RotateSystem : ComponentSystem
    {
        public override void OnUpdate(float deltaTime)
        {
            foreach (Entity entity in entities)
            {
                Engine.ECS.Do<RotationComponent>(entity, x =>
                {
                    x.Rotate(Vector3.UnitY, 45 * deltaTime);
                    return x;
                });
            }
        }
    }
}