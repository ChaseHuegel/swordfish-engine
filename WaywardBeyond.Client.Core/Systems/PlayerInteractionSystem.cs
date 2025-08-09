using System;
using System.Numerics;
using Shoal.DependencyInjection;
using Shoal.Modularity;
using Swordfish.Bricks;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Constraints;
using Swordfish.Library.IO;
using Swordfish.Physics;
using Swordfish.Types;
using Swordfish.UI;
using Swordfish.UI.Elements;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Debug;

namespace WaywardBeyond.Client.Core.Systems;

internal sealed class PlayerInteractionSystem : IEntryPoint
{
    private readonly IInputService _inputService;
    private readonly IPhysics _physics;
    private readonly IRenderContext _renderContext;
    private readonly IWindowContext _windowContext;
    private readonly BrickEntityBuilder _brickEntityBuilder;
    private readonly CubeGizmo _cubeGizmo;
    private readonly TextElement _debugText;
    
    public PlayerInteractionSystem(
        in IInputService inputService,
        in IPhysics physics,
        in ILineRenderer lineRenderer,
        in IRenderContext renderContext,
        in IWindowContext windowContext,
        in IUIContext uiContext,
        in BrickEntityBuilder brickEntityBuilder)
    {
        _inputService = inputService;
        _physics = physics;
        _renderContext = renderContext;
        _windowContext = windowContext;
        _brickEntityBuilder = brickEntityBuilder;
        _cubeGizmo = new CubeGizmo(lineRenderer, Vector4.One);
        
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
    
    public void Run()
    {
        _physics.FixedUpdate += OnFixedUpdate;
        _inputService.Clicked += OnClicked;
    }

    private void OnClicked(object? sender, ClickedEventArgs e)
    {
        if (e.MouseButton == MouseButton.Left)
        {
            OnLeftClick();
        }
        
        if (e.MouseButton == MouseButton.Right)
        {
            OnRightClick();
        }
    }

    private void OnLeftClick()
    {
        if (!TryGetBrickFromScreenSpace(false, out Entity entity, out Brick clickedBrick, out (int X, int Y, int Z) brickPos, out BrickComponent brickComponent, out TransformComponent transformComponent))
        {
            return;
        }
        
        brickComponent.Grid.Set(brickPos.X, brickPos.Y, brickPos.Z, Brick.Empty);
        _brickEntityBuilder.Rebuild(entity.Ptr);
    }
    
    private void OnRightClick()
    {
        if (!TryGetBrickFromScreenSpace(true, out Entity entity, out Brick clickedBrick, out (int X, int Y, int Z) brickPos, out BrickComponent brickComponent, out TransformComponent transformComponent))
        {
            return;
        }
        
        brickComponent.Grid.Set(brickPos.X, brickPos.Y, brickPos.Z, BrickRegistry.MetalPanel);
        _brickEntityBuilder.Rebuild(entity.Ptr);
    }

    private void OnFixedUpdate(object? sender, EventArgs e)
    {
        if (!TryGetBrickFromScreenSpace(false, out Entity entity, out Brick clickedBrick, out (int X, int Y, int Z) brickPos, out BrickComponent brickComponent, out TransformComponent transformComponent))
        {
            return;
        }
        
        Vector3 worldPos = BrickToWorldSpace(brickPos, transformComponent.Position, transformComponent.Orientation);
        _cubeGizmo.Render(new TransformComponent(worldPos, transformComponent.Orientation));
        _debugText.Text = $"CenterOfMass:{brickComponent.Grid.CenterOfMass}\nHovering: {clickedBrick}\nBrick: {brickPos}\nWorld: {worldPos.X}, {worldPos.Y}, {worldPos.Z}";
    }

    private bool TryGetBrickFromScreenSpace(bool offset, out Entity entity, out Brick clickedBrick, out (int X, int Y, int Z) brickPos, out BrickComponent brickComponent, out TransformComponent transformComponent)
    {
        Camera camera = _renderContext.Camera.Get();
        Vector2 cursorPos = _inputService.CursorPosition;
        Ray ray = camera.ScreenPointToRay((int)cursorPos.X, (int)cursorPos.Y, (int)_windowContext.Resolution.X, (int)_windowContext.Resolution.Y);
        RaycastResult raycast = _physics.Raycast(ray * 1000);
        
        if (!raycast.Hit || !raycast.Entity.TryGet(out brickComponent) || !raycast.Entity.TryGet(out transformComponent))
        {
            entity = default;
            clickedBrick = default;
            brickPos = default;
            brickComponent = default;
            transformComponent = default;
            return false;
        }

        Vector3 hitPoint;
        if (offset)
        {
            hitPoint = raycast.Point + raycast.Normal * 0.1f;
        }
        else
        {
            hitPoint = raycast.Point - raycast.Normal * 0.1f;
        }

        brickPos = WorldToBrickSpace(hitPoint, transformComponent.Position, transformComponent.Orientation);
        clickedBrick = brickComponent.Grid.Get(brickPos.X, brickPos.Y, brickPos.Z);
        entity = raycast.Entity;
        return true;
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