using Swordfish.ECS;

namespace Swordfish.Graphics;

public class MeshRendererSystem(in IRenderContext renderContext) : EntitySystem<MeshRendererComponent, TransformComponent>
{
    public override int Order => 100_000;

    private readonly IRenderContext _renderContext = renderContext;

    protected override void OnTick(float delta, DataStore store, int entity, ref MeshRendererComponent rendererComponent, ref TransformComponent transformComponent)
    {
        //  The component should always have a valid renderer because of the ctor,
        //  but it is possible to init without one (ex: using `default`).
        MeshRenderer? meshRenderer = rendererComponent.MeshRenderer;
        if (meshRenderer == null)
        {
            return;
        }
        
        if (!rendererComponent.Bound)
        {
            _renderContext.Bind(meshRenderer, entity);
            rendererComponent.Bound = true;
        }

        meshRenderer.Transform.Update(transformComponent.Position, transformComponent.Orientation,transformComponent.Scale);
    }
}
