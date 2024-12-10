using System;
using System.Numerics;
using Swordfish.Bricks;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using Swordfish.Library.Types;
using Swordfish.Physics;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Debug;

namespace WaywardBeyond.Client.Core.Systems;

internal sealed class PlayerInteractionSystem
    : EntitySystem<PlayerComponent, TransformComponent>
{
    private readonly IInputService _inputService;
    private readonly IPhysics _physics;
    private readonly IRenderContext _renderContext;
    private readonly IWindowContext _windowContext;
    private readonly CubeGizmo _cubeGizmo;
    
    private TransformComponent? _transform;

    public PlayerInteractionSystem(in IInputService inputService, in IPhysics physics, in ILineRenderer lineRenderer, in IRenderContext renderContext, in IWindowContext windowContext)
    {
        _inputService = inputService;
        _physics = physics;
        _renderContext = renderContext;
        _windowContext = windowContext;
        _cubeGizmo = new CubeGizmo(lineRenderer);
        
        _physics.FixedUpdate += OnFixedUpdate;
    }

    private void OnFixedUpdate(object? sender, EventArgs e)
    {
        if (_transform == null)
        {
            return;
        }
        
        Camera camera = _renderContext.Camera.Get();
        Transform transform = camera.Transform;
        Vector2 cursorPos = _inputService.CursorPosition;
        Ray ray = camera.ScreenPointToRay((int)cursorPos.X, (int)cursorPos.Y, (int)_windowContext.Resolution.X, (int)_windowContext.Resolution.Y) * 1000;
        RaycastResult raycast = _physics.Raycast(ray);
        
        if (!raycast.Hit)
        {
            return;
        }
        
        if (!raycast.Entity.TryGet(out BrickComponent brickComponent) || !raycast.Entity.TryGet(out TransformComponent transformComponent))
        {
            return;
        }

        Quaternion invertedOrientation = Quaternion.Inverse(transform.Orientation);
        Vector3 localPoint = Vector3.Transform(raycast.Point - transform.Position, invertedOrientation);
        var x = (int)localPoint.X;
        var y = (int)localPoint.Y;
        var z = (int)localPoint.Z;
        Brick clickedBrick = brickComponent.Grid.Get(x, y, z);
        
        _cubeGizmo.Render(new TransformComponent(transform.Position + new Vector3(x, y, z), transformComponent.Orientation));
    }

    protected override void OnTick(float delta, DataStore store, int entity, ref PlayerComponent player, ref TransformComponent transform)
    {
        _transform = transform;
    }
}