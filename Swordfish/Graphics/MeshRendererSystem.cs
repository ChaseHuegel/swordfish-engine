using Swordfish.ECS;

namespace Swordfish.Graphics;

[ComponentSystem(typeof(MeshRendererComponent), typeof(TransformComponent))]
public class MeshRendererSystem : ComponentSystem
{
    protected override void Update(Entity entity, float deltaTime)
    {
        MeshRendererComponent renderComponent = entity.World.Store.GetAt<MeshRendererComponent>(entity.Ptr, MeshRendererComponent.DefaultIndex);
        TransformComponent transform = entity.World.Store.GetAt<TransformComponent>(entity.Ptr, TransformComponent.DefaultIndex);

        if (!renderComponent.Bound)
        {
            SwordfishEngine.Kernel.Get<IRenderContext>().Bind(renderComponent.MeshRenderer!);
            renderComponent.Bound = true;
        }

        renderComponent.MeshRenderer!.Transform.Position = transform.Position;
        renderComponent.MeshRenderer!.Transform.Rotation = transform.Rotation;
    }
}
