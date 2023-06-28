using System.Numerics;

namespace Swordfish.ECS;

[ComponentSystem(typeof(TransformComponent))]
public class RoundaboutSystem : ComponentSystem
{
    protected override void Update(Entity entity, float deltaTime)
    {
        TransformComponent transform = entity.World.Store.GetAt<TransformComponent>(entity.Ptr, TransformComponent.DefaultIndex);
        transform.Rotation += new Vector3(0, deltaTime, 0);
    }
}
