using Swordfish.ECS;

namespace Swordfish.Graphics;

[ComponentSystem(typeof(MeshRendererComponent), typeof(TransformComponent))]
public class MeshRendererSystem : ComponentSystem
{
    //  TODO ECS needs to be refactored to using DI
    private static IRenderContext RenderContext => renderContext ??= SwordfishEngine.Kernel.Get<IRenderContext>();
    private static IRenderContext? renderContext;

    protected override void OnUpdated()
    {
        var renderContext = SwordfishEngine.Kernel.Get<IRenderContext>();
        renderContext.RefreshRenderTargets();
    }

    protected override void Update(Entity entity, float deltaTime)
    {
        MeshRendererComponent renderComponent = entity.World.Store.GetAt<MeshRendererComponent>(entity.Ptr, MeshRendererComponent.DefaultIndex);
        TransformComponent transform = entity.World.Store.GetAt<TransformComponent>(entity.Ptr, TransformComponent.DefaultIndex);

        if (!renderComponent.Bound)
        {
            renderComponent.Bound = true;
            RenderContext.Bind(renderComponent.MeshRenderer!);
        }

        renderComponent.MeshRenderer!.Transform.Position = transform.Position;
        renderComponent.MeshRenderer!.Transform.Rotation = transform.Rotation;
    }
}
