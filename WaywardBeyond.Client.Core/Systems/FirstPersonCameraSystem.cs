using Swordfish.ECS;
using Swordfish.Graphics;
using WaywardBeyond.Client.Core.Components;

namespace WaywardBeyond.Client.Core.Systems;

internal sealed class FirstPersonCameraSystem(in IRenderContext renderContext)
    : EntitySystem<PlayerComponent, TransformComponent>
{
    private readonly Camera _camera = renderContext.Camera.Get();

    protected override void OnTick(float delta, DataStore store, int entity, ref PlayerComponent player, ref TransformComponent transform)
    {
        _camera.Transform.Position = transform.Position;
        _camera.Transform.Orientation = transform.Orientation;
    }
}