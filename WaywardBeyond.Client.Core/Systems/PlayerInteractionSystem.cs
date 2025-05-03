using System;
using System.Numerics;
using Swordfish.Bricks;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Constraints;
using Swordfish.Library.IO;
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

        (int X, int Y, int Z) brickPos = WorldToBrickSpace(raycast.Point + ray.Vector * 0.1f, transformComponent.Position, transformComponent.Orientation);
        Vector3 worldPos = BrickToWorldSpace(brickPos, transformComponent.Position, transformComponent.Orientation);
        
        Brick clickedBrick = brickComponent.Grid.Get(brickPos.X, brickPos.Y, brickPos.Z);

        _cubeGizmo.Render(new TransformComponent(worldPos, transformComponent.Orientation));
        _debugText.Text = $"CenterOfMass:{brickComponent.Grid.CenterOfMass}\nHovering: {clickedBrick}\nBrick: {brickPos}\nPoint: {raycast.Point.X}, {raycast.Point.Y}, {raycast.Point.Z}";
    }

    protected override void OnTick(float delta, DataStore store, int entity, ref PlayerComponent player, ref TransformComponent transform)
    {
    }

    private static (int X, int Y, int Z) WorldToBrickSpace(Vector3 hitPoint, Vector3 gridOrigin, Quaternion gridRotation)
    {
        Vector3 localPoint = Vector3.Transform(hitPoint - gridOrigin, Quaternion.Inverse(gridRotation)) + new Vector3(0.5f);
        
        var x = (int)Math.Floor(localPoint.X);
        var y = (int)Math.Floor(localPoint.Y);
        var z = (int)Math.Floor(localPoint.Z);

        return (x, y, z);
    }

    private static Vector3 BrickToWorldSpace((int X, int Y, int Z) cellCoordinates, Vector3 gridOrigin, Quaternion gridRotation)
    {
        var localCenter = new Vector3(
            cellCoordinates.X,
            cellCoordinates.Y,
            cellCoordinates.Z
        );

        return Vector3.Transform(localCenter, gridRotation) + gridOrigin;
    }
}