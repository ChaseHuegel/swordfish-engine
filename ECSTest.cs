using System;
using System.Globalization;
using OpenTK.Mathematics;
using Swordfish;
using Swordfish.ECS;
using Swordfish.Rendering;

public class ECSTest
{
    [Component]
    public struct PositionComponent
    {
        public Vector3 position;
    }

    [Component]
    public struct RotationComponent
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
    public struct RenderComponent
    {
        public Mesh mesh;
    }

    [ComponentSystem(typeof(PositionComponent))]
    public class MoveSystem : ComponentSystem
    {
        public override void Start()
        {

        }

        public override void Destroy()
        {

        }

        public override void Update()
        {

        }
    }

    [ComponentSystem(typeof(PositionComponent), typeof(RotationComponent), typeof(RenderComponent))]
    public class RenderSystem : ComponentSystem
    {
        Entity[] entities;

        public override void Start()
        {
            entities = Engine.ECS.GetEntitiesWith(typeof(PositionComponent), typeof(RotationComponent), typeof(RenderComponent));

            foreach (Entity e in entities)
                Engine.Renderer.Push(e);
        }

        public override void Destroy()
        {

        }

        public override void Update()
        {
            entities = Engine.ECS.GetEntitiesWith(typeof(PositionComponent), typeof(RotationComponent), typeof(RenderComponent));

            foreach (Entity e in entities)
            {
                e.SetData<RotationComponent>(
                    e.GetData<RotationComponent>().Rotate(Vector3.UnitY, 40 * Engine.DeltaTime)
                );
            }
        }
    }
}