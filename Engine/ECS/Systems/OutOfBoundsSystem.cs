using OpenTK.Mathematics;

namespace Swordfish.Engine.ECS
{
    [ComponentSystem(typeof(RigidbodyComponent), typeof(TransformComponent))]
    public class OutOfBoundsSystem : ComponentSystem
    {
        public override void OnUpdateEntity(float deltaTime, Entity entity)
        {
            Vector3 pos = Swordfish.ECS.Get<TransformComponent>(entity).position;

            //  Destroy entities which leave the physics boundry
            if (!Swordfish.Physics.InBounds(pos))
                Swordfish.ECS.DestroyEntity(entity);
        }
    }
}