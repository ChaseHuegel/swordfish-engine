using Swordfish.ECS;
using Swordfish.Library.Extensions;

namespace Swordfish.Graphics;

[ComponentSystem(typeof(MeshRendererComponent), typeof(TransformComponent))]
public class MeshRendererSystem : ComponentSystem
{
    private static IRenderContext RenderContext => renderContext ??= SwordfishEngine.SyncManager.WaitForResult(SwordfishEngine.Kernel.Get<IRenderContext>);
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
            RenderContext.Bind(renderComponent.MeshRenderer!);
            renderComponent.Bound = true;
        }

        renderComponent.MeshRenderer!.Transform.Position = transform.Position;
        renderComponent.MeshRenderer!.Transform.Rotation = transform.Rotation;
    }
}
