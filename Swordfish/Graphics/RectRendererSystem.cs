using Swordfish.ECS;

namespace Swordfish.Graphics;

public class RectRendererSystem(in IRenderContext renderContext) : IEntitySystem
{
    private readonly IRenderContext _renderContext = renderContext;

    public void Tick(float delta, DataStore store)
    {
        var action = new BindRenderComponentAction(this);
        store.QueryRef<RectRendererComponent, BindRenderComponentAction>(delta, ref action);
    }
    
    private readonly struct BindRenderComponentAction(in RectRendererSystem owner) : IForEachRef<RectRendererComponent>
    {
        private readonly RectRendererSystem _owner = owner;

        public void Execute(float delta, DataStore store, int entity, ref Ref<RectRendererComponent> rendererComponent)
        {
            if (rendererComponent.Read.Bound)
            {
                return;
            }

            rendererComponent.Write.Bound = true;
            _owner._renderContext.Bind(rendererComponent.Read.RectRenderer);
        }
    }
}