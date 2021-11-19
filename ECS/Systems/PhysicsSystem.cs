using System;
using OpenTK.Mathematics;

namespace Swordfish.ECS
{
    [ComponentSystem(typeof(RigidbodyComponent), typeof(TransformComponent))]
    public class PhysicsSystem : ComponentSystem
    {
        public override void OnPullEntities() => Engine.Physics.PushBodies(entities);

        public override void OnUpdateEntity(float deltaTime, Entity entity)
        {
            Vector3 pos = Engine.ECS.Get<TransformComponent>(entity).position;

            //  TODO this is for testing purposes, physics should just ignore or disable anything out of bounds
            //  Destroy entities which leave the physics boundry
            if (!Engine.Physics.InBounds(pos))
                Engine.ECS.DestroyEntity(entity);
        }
    }
}