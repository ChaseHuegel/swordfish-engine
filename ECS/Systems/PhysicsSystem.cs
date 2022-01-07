using System;
using OpenTK.Mathematics;

namespace Swordfish.ECS
{
    [ComponentSystem(typeof(RigidbodyComponent), typeof(TransformComponent))]
    public class PhysicsSystem : ComponentSystem
    {
        public override void OnPullEntities() => Engine.Physics.PushBodies(entities);
    }
}