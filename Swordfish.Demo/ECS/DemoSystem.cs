using Swordfish.ECS;

namespace Swordfish.Demo.ECS;

[ComponentSystem(typeof(DemoComponent))]
public class DemoSystem : ComponentSystem
{
    protected override void Update(Entity entity, float deltaTime)
    {
        entity.TryGetComponent(DemoComponent.Index, out DemoComponent? demoComponent);

        if (demoComponent != null)
        {
            demoComponent.Value = 30;
        }
    }
}
