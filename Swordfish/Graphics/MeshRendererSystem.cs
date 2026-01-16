using Swordfish.ECS;

namespace Swordfish.Graphics;

public class MeshRendererSystem(in IRenderer renderer) : EntitySystem<MeshRendererComponent, TransformComponent>
{
    private readonly IRenderer _renderer = renderer;

    protected override void OnTick(float delta, DataStore store, int entity, ref MeshRendererComponent rendererComponent, ref TransformComponent transformComponent)
    {
        if (rendererComponent.Bound)
        {
            return;
        }
        
        //  The component should always have a valid renderer because of the ctor,
        //  but it is possible to init without one (ex: using `default`).
        MeshRenderer? meshRenderer = rendererComponent.MeshRenderer;
        if (meshRenderer == null)
        {
            return;
        }
        
        _renderer.Bind(meshRenderer, entity);
        rendererComponent.Bound = true;
    }
}
