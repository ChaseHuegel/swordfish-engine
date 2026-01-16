using Swordfish.ECS;

namespace Swordfish.Graphics;

public class RectRendererSystem(in IRenderContext renderContext) : EntitySystem<RectRendererComponent>
{
    private readonly IRenderContext _renderContext = renderContext;

    protected override void OnTick(float delta, DataStore store, int entity, ref RectRendererComponent rendererComponent)
    {
        if (rendererComponent.Bound)
        {
            return;
        }

        rendererComponent.Bound = true;
        _renderContext.Bind(rendererComponent.RectRenderer);
    }
}
