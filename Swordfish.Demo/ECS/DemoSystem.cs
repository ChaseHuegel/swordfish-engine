using Swordfish.ECS;

namespace Swordfish.Demo.ECS;

[ComponentSystem(typeof(DemoComponent))]
public class DemoSystem : ComponentSystem
{
    protected override void Update(Entity entity, float deltaTime)
    {
        DemoComponent? demoComponent = entity.GetComponent<DemoComponent>(DemoComponent.Index);

        if (demoComponent != null)
        {
            demoComponent.Field = 30;
        }
    }
}
