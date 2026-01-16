using Swordfish.ECS;

namespace Swordfish.Graphics;

public class RectRendererSystem(in IRenderer renderer) : EntitySystem<RectRendererComponent>
{
    private readonly IRenderer _renderer = renderer;

    protected override void OnTick(float delta, DataStore store, int entity, ref RectRendererComponent rendererComponent)
    {
        if (rendererComponent.Bound)
        {
            return;
        }

        rendererComponent.Bound = true;
        _renderer.Bind(rendererComponent.RectRenderer);
    }
}
