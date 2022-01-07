using System;
using OpenTK.Mathematics;

namespace Swordfish.ECS
{
    [ComponentSystem(typeof(RigidbodyComponent), typeof(TransformComponent))]
    public class OutOfBoundsSystem : ComponentSystem
    {
        public override void OnUpdateEntity(float deltaTime, Entity entity)
        {
            Vector3 pos = Engine.ECS.Get<TransformComponent>(entity).position;

            //  Destroy entities which leave the physics boundry
            if (!Engine.Physics.InBounds(pos))
                Engine.ECS.DestroyEntity(entity);
        }
    }
}