using System.Numerics;

namespace Swordfish.ECS;

[ComponentSystem(typeof(PhysicsComponent), typeof(TransformComponent))]
public class PhysicsSystem : ComponentSystem
{
    protected override void Update(Entity entity, float deltaTime)
    {
        PhysicsComponent? physics = entity.GetComponent<PhysicsComponent>(PhysicsComponent.DefaultIndex);
        TransformComponent? transform = entity.GetComponent<TransformComponent>(TransformComponent.DefaultIndex);

        if (physics != null && transform != null)
        {
            physics.Velocity.Y = -9.8f;
            transform.Position += physics.Velocity;
        }
    }
}
