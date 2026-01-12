using Swordfish.ECS;
using Swordfish.Graphics;
using WaywardBeyond.Client.Core.Components;

namespace WaywardBeyond.Client.Core.Systems;

internal sealed class FirstPersonCameraSystem(in IRenderContext renderContext)
    : EntitySystem<PlayerComponent, TransformComponent>
{
    private readonly Camera _camera = renderContext.Camera.Get();
    
    public override int Order => 100_000;

    protected override void OnTick(float delta, DataStore store, int entity, ref PlayerComponent player, ref TransformComponent transform)
    {
        lock (_camera)
        {
            _camera.Transform.Update(transform.Position, transform.Orientation);
        }
    }
}