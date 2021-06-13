using System;
using System.Globalization;
using OpenTK.Mathematics;
using Swordfish;
using Swordfish.ECS;
using Swordfish.Rendering;

namespace ECSTest
{
    [ComponentSystem(typeof(PositionComponent))]
    public class GravitySystem : ComponentSystem
    {
        public override void OnUpdate()
        {
            foreach (Entity entity in entities)
            {
                Engine.ECS.Do<PositionComponent>(entity, x =>
                {
                    x.position -= Vector3.UnitY * Engine.DeltaTime;
                    return x;
                });
            }
        }
    }

    [ComponentSystem(typeof(RotationComponent))]
    public class RotateSystem : ComponentSystem
    {
        public override void OnUpdate()
        {
            foreach (Entity entity in entities)
            {
                Engine.ECS.Do<RotationComponent>(entity, x =>
                {
                    x.Rotate(Vector3.UnitY, 45 * Engine.DeltaTime);
                    return x;
                });
            }
        }
    }
}