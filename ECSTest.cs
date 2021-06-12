using System;
using System.Globalization;
using OpenTK.Mathematics;
using Swordfish;
using Swordfish.ECS;
using Swordfish.Rendering;

namespace ECSTest
{
    [Component]
    struct PositionComponent
    {
        public Vector3 position;
    }

    [Component]
    struct RotationComponent
    {
        public Quaternion orientation;
        public Vector3 forward;
        public Vector3 right;
        public Vector3 up;

        public RotationComponent Rotate(Vector3 axis, float angle)
        {
            orientation = Quaternion.FromAxisAngle(orientation * axis, MathHelper.DegreesToRadians(-angle)) * orientation;

            forward = Vector3.Transform(-Vector3.UnitZ, orientation);
            right = Vector3.Transform(-Vector3.UnitX, orientation);
            up = Vector3.Transform(Vector3.UnitY, orientation);

            return this;
        }
    }

    [Component]
    struct RenderComponent
    {
        public Mesh mesh;
    }

    [ComponentSystem(typeof(PositionComponent))]
    public class GravitySystem : ComponentSystem
    {
        public override void OnStart() {}
        public override void OnShutdown() {}

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
        public override void OnStart() {}
        public override void OnShutdown() {}

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