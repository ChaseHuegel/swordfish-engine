using Swordfish.ECS;

namespace Swordfish.Graphics;

public class MeshRendererSystem(in IRenderContext renderContext) : EntitySystem<MeshRendererComponent, TransformComponent>
{
    private readonly IRenderContext _renderContext = renderContext;

    protected override void OnTick(float delta, DataStore store, int entity, ref MeshRendererComponent rendererComponent, ref TransformComponent transformComponent)
    {
        if (!rendererComponent.Bound)
        {
            rendererComponent.Bound = true;
            _renderContext.Bind(rendererComponent.MeshRenderer!);
        }

        rendererComponent.MeshRenderer.Transform.Position = transformComponent.Position;
        rendererComponent.MeshRenderer.Transform.Orientation = transformComponent.Orientation;
        rendererComponent.MeshRenderer.Transform.Scale = transformComponent.Scale;
    }
}
