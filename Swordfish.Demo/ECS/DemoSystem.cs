using Swordfish.ECS;

namespace Swordfish.Demo.ECS;

[ComponentSystem(typeof(DemoComponent))]
public class DemoSystem : ComponentSystem
{
    public static int ComponentIndex { get; set; }

    protected override void Update(Entity entity, float deltaTime)
    {
        entity.TryGetComponent(ComponentIndex, out DemoComponent? demoComponent);

        if (demoComponent != null)
        {
            demoComponent.Value = 30;
        }
    }
}
