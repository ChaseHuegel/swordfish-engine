namespace Swordfish.ECS;

[ComponentSystem(typeof(PhysicsComponent), typeof(TransformComponent))]
public class PhysicsSystem : ComponentSystem
{
    protected override void Update(Entity entity, float deltaTime)
    {
        // PhysicsComponent physics = entity.World.Store.GetAt<PhysicsComponent>(entity.Ptr, PhysicsComponent.DefaultIndex);
        // TransformComponent transform = entity.World.Store.GetAt<TransformComponent>(entity.Ptr, TransformComponent.DefaultIndex);

        // physics.Velocity.Y = -9.8f;
        // transform.Position += physics.Velocity * deltaTime;
    }
}
