using System;
using System.Numerics;
using Swordfish.Bricks;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Constraints;
using Swordfish.Library.IO;
using Swordfish.Library.Types;
using Swordfish.Physics;
using Swordfish.Types;
using Swordfish.UI;
using Swordfish.UI.Elements;
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
    private readonly TextElement _debugText;
    
    public PlayerInteractionSystem(in IInputService inputService,
        in IPhysics physics,
        in ILineRenderer lineRenderer,
        in IRenderContext renderContext,
        in IWindowContext windowContext,
        in IUIContext uiContext)
    {
        _inputService = inputService;
        _physics = physics;
        _renderContext = renderContext;
        _windowContext = windowContext;
        _cubeGizmo = new CubeGizmo(lineRenderer, Vector4.One);
        
        _physics.FixedUpdate += OnFixedUpdate;
        
        _debugText = new TextElement("");
        _ = new CanvasElement(uiContext, windowContext, "Debug")
        {
            Constraints = new RectConstraints
            {
                Anchor = ConstraintAnchor.TOP_LEFT,
                Width = new AbsoluteConstraint(300),
                Height = new AbsoluteConstraint(120),
            },
            Content = {
                new PanelElement("Interaction")
                {
                    Constraints = new RectConstraints
                    {
                        Width = new FillConstraint(),
                        Height = new FillConstraint(),
                    },
                    Content = {
                        _debugText,
                    },
                },
            },
        };
    }

    private void OnFixedUpdate(object? sender, EventArgs e)
    {
        Camera camera = _renderContext.Camera.Get();
        Transform transform = camera.Transform;
        Vector2 cursorPos = _inputService.CursorPosition;
        Ray ray = camera.ScreenPointToRay((int)cursorPos.X, (int)cursorPos.Y, (int)_windowContext.Resolution.X, (int)_windowContext.Resolution.Y);
        RaycastResult raycast = _physics.Raycast(ray * 1000);
        
        if (!raycast.Hit)
        {
            return;
        }
        
        if (!raycast.Entity.TryGet(out BrickComponent brickComponent) || !raycast.Entity.TryGet(out TransformComponent transformComponent))
        {
            return;
        }

        //  TODO normalizing the orientation isn't working correctly. The point calculation below only works if the target isn't rotated.
        Quaternion invertedOrientation = Quaternion.Inverse(transformComponent.Orientation);
        Vector3 offset = new Vector3(brickComponent.Grid.DimensionSize / 2f);
        Vector3 localPoint = Vector3.Transform(raycast.Point - transformComponent.Position + offset, invertedOrientation);
        localPoint += new Vector3(0.5f);      //  Offset by half a unit to account for the brick center.
        localPoint += ray.Vector * 0.1f;    //  Penetrate slightly to select the target
        
        //  TODO figure out a better way to handle this.
        //  World to brick coordinate is handled by dropping the floating point.
        //  For negative coordinates, it must be offset by 1. ex: -0.1 evaluates as -1, and 0.1 evaluates 0.
        bool negX = localPoint.X < 0;
        bool negY = localPoint.Y < 0;
        bool negZ = localPoint.Z < 0;
        var x = (int)(negX ? localPoint.X - 1 : localPoint.X);
        var y = (int)(negY ? localPoint.Y - 1 : localPoint.Y);
        var z = (int)(negZ ? localPoint.Z - 1 : localPoint.Z);
        Brick clickedBrick = brickComponent.Grid.Get(x, y, z);  //  TODO this isn't quite correct. The coords are slightly offset.
        
        _cubeGizmo.Render(new TransformComponent(transformComponent.Position + new Vector3(x, y, z) - offset, transformComponent.Orientation));

        _debugText.Text = $"Hovering: {clickedBrick}\nBrick: {x}, {y}, {z}\nLocal: {localPoint.X}, {localPoint.Y}, {localPoint.Z}\nPoint: {raycast.Point.X}, {raycast.Point.Y}, {raycast.Point.Z}";
    }

    protected override void OnTick(float delta, DataStore store, int entity, ref PlayerComponent player, ref TransformComponent transform)
    {
    }
}