using System;
using System.Numerics;
using Shoal.DependencyInjection;
using Shoal.Modularity;
using Swordfish.Bricks;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.Constraints;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using Swordfish.Physics;
using Swordfish.Types;
using Swordfish.UI;
using Swordfish.UI.Elements;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Debug;
using WaywardBeyond.Client.Core.Items;
using WaywardBeyond.Client.Core.Player;
using WaywardBeyond.Client.Core.UI;

namespace WaywardBeyond.Client.Core.Systems;

internal sealed class PlayerInteractionService : IEntryPoint
{
    private readonly IInputService _inputService;
    private readonly IPhysics _physics;
    private readonly IRenderContext _renderContext;
    private readonly IWindowContext _windowContext;
    private readonly BrickEntityBuilder _brickEntityBuilder;
    private readonly IECSContext _ecsContext;
    private readonly PlayerData _playerData;
    private readonly BrickDatabase _brickDatabase;
    private readonly ItemDatabase _itemDatabase;
    private readonly ShapeSelector _shapeSelector;
    private readonly OrientationSelector _orientationSelector;
    private readonly CubeGizmo _cubeGizmo;
    private readonly TextElement _debugText;
    
    private int _rotation = 0;
    
    public PlayerInteractionService(
        in IInputService inputService,
        in IPhysics physics,
        in ILineRenderer lineRenderer,
        in IRenderContext renderContext,
        in IWindowContext windowContext,
        in IUIContext uiContext,
        in BrickEntityBuilder brickEntityBuilder,
        in IECSContext ecsContext,
        in PlayerData playerData,
        in BrickDatabase brickDatabase,
        in ItemDatabase itemDatabase,
        in ShapeSelector shapeSelector,
        in OrientationSelector orientationSelector
    ) {
        _inputService = inputService;
        _physics = physics;
        _renderContext = renderContext;
        _windowContext = windowContext;
        _brickEntityBuilder = brickEntityBuilder;
        _ecsContext = ecsContext;
        _playerData = playerData;
        _brickDatabase = brickDatabase;
        _itemDatabase = itemDatabase;
        _shapeSelector = shapeSelector;
        _orientationSelector = orientationSelector;
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

        inputService.Scrolled += OnScrolled;
    }

    public void Run()
    {
        _physics.FixedUpdate += OnFixedUpdate;
        _inputService.Clicked += OnClicked;
    }
    
    private void OnScrolled(object? sender, ScrolledEventArgs e)
    {
        if (!_inputService.IsKeyHeld(Key.Shift))
        {
            return;
        }
        
        double scrollDelta = Math.Round(e.Delta, MidpointRounding.AwayFromZero);
        _rotation = MathS.WrapInt(_rotation + (int)scrollDelta, 0, 3);
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
        
        if (e.MouseButton == MouseButton.Middle)
        {
            OnMiddleClick();
        }
    }

    private void OnLeftClick()
    {
        if (!TryGetBrickFromScreenSpace(false, out Entity clickedEntity, out Brick clickedBrick, out (int X, int Y, int Z) brickPos, out BrickComponent brickComponent, out TransformComponent transformComponent))
        {
            return;
        }
        
        SetBrick(clickedEntity.Ptr, brickComponent.Grid, brickPos.X, brickPos.Y, brickPos.Z, Brick.Empty);
        _brickEntityBuilder.Rebuild(clickedEntity.Ptr);
        
        _ecsContext.World.DataStore.Query<PlayerComponent, InventoryComponent>(0f, PlayerInventoryQuery);
        return;

        void PlayerInventoryQuery(float delta, DataStore store, int playerEntity, ref PlayerComponent player, ref InventoryComponent inventory)
        {
            Result<BrickInfo> brickInfoResult = _brickDatabase.Get(clickedBrick.ID);
            if (brickInfoResult.Success)
            {
                inventory.Add(new ItemStack(brickInfoResult.Value.ID));
            }
        }
    }

    private void OnRightClick()
    {
        if (!TryGetBrickFromScreenSpace(true, out Entity clickedEntity, out Brick clickedBrick, out (int X, int Y, int Z) brickPos, out BrickComponent brickComponent, out TransformComponent transformComponent))
        {
            return;
        }

        Brick? brickToPlace = null;
        _ecsContext.World.DataStore.Query<PlayerComponent, InventoryComponent>(0f, TryConsumeItemQuery);
        if (!brickToPlace.HasValue)
        {
            return;
        }

        BrickOrientation orientation = _orientationSelector.SelectedOrientation.Get();
        orientation.YawRotations -= _rotation;
        brickToPlace = brickToPlace.Value with { Orientation = orientation};

        SetBrick(clickedEntity.Ptr, brickComponent.Grid, brickPos.X, brickPos.Y, brickPos.Z, brickToPlace.Value);
        _brickEntityBuilder.Rebuild(clickedEntity.Ptr);
        return;

        void TryConsumeItemQuery(float delta, DataStore store, int playerEntity, ref PlayerComponent player,
            ref InventoryComponent inventory)
        {
            Result<ItemSlot> mainHandResult = _playerData.GetMainHand(store, playerEntity, inventory);
            if (!mainHandResult.Success || mainHandResult.Value.Item.Placeable == null)
            {
                return;
            }

            ItemSlot mainHand = mainHandResult.Value;
            Item item = mainHand.Item;
            PlaceableDefinition placeable = item.Placeable.Value;

            if (placeable.Type != PlaceableType.Brick)
            {
                return;
            }

            Result<BrickInfo> brickInfoResult = _brickDatabase.Get(placeable.ID);
            if (!brickInfoResult.Success)
            {
                return;
            }

            if (!inventory.Remove(mainHand.Slot, 1))
            {
                return;
            }

            BrickInfo brickInfo = brickInfoResult.Value;
            Brick brick = brickInfo.GetBrick();

            //  If this brick supports changing shapes, use the currently selected shape.
            if (brickInfo.Shape == BrickShape.Any)
            {
                brick.Data = (byte)_shapeSelector.SelectedShape.Get();
            }
            
            brickToPlace = brick;
        }
    }
    
    private void OnMiddleClick()
    {
        if (!TryGetBrickFromScreenSpace(false, out Entity clickedEntity, out Brick clickedBrick, out (int X, int Y, int Z) brickPos, out BrickComponent brickComponent, out TransformComponent transformComponent))
        {
            return;
        }

        //  Clone the target brick's shape
        _shapeSelector.SelectedShape.Set((BrickShape)clickedBrick.Data);
        
        //  If the player has a valid item, select it
        _ecsContext.World.DataStore.Query<PlayerComponent, InventoryComponent>(0f, PlayerInventoryQuery);
        void PlayerInventoryQuery(float delta, DataStore store, int playerEntity, ref PlayerComponent player, ref InventoryComponent inventory)
        {
            Result<BrickInfo> brickInfoResult = _brickDatabase.Get(clickedBrick.ID);
            if (!brickInfoResult.Success)
            {
                return;
            }

            for (var i = 0; i < inventory.Contents.Length; i++)
            {
                string itemID = inventory.Contents[i].ID;
                Result<Item> itemResult = _itemDatabase.Get(itemID);
                if (!itemResult.Success)
                {
                    return;
                }

                if (itemResult.Value.Placeable == null)
                {
                    return;
                }

                Result<BrickInfo> placeableBrickResult =  _brickDatabase.Get(itemResult.Value.Placeable.Value.ID);
                if (!placeableBrickResult.Success)
                {
                    return;
                }

                if (placeableBrickResult.Value.DataID != clickedBrick.ID)
                {
                    continue;
                }

                store.Query<EquipmentComponent>(playerEntity, 0f, UpdateActiveSlotQuery);
                void UpdateActiveSlotQuery(float _, DataStore dataStore, int entity, ref EquipmentComponent equipment)
                {
                    equipment.ActiveInventorySlot = i;
                }
                break;
            }
        }
    }

    private void OnFixedUpdate(object? sender, EventArgs e)
    {
        if (!TryGetBrickFromScreenSpace(false, out Entity entity, out Brick clickedBrick, out (int X, int Y, int Z) brickPos, out BrickComponent brickComponent, out TransformComponent transformComponent))
        {
            _cubeGizmo.Visible = false;
            return;
        }
        
        Vector3 worldPos = BrickToWorldSpace(brickPos, transformComponent.Position, transformComponent.Orientation);
        _cubeGizmo.Visible = true;
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
    
    private void SetBrick(int entity, BrickGrid grid, int x, int y, int z, Brick brick)
    {
        Brick currentBrick = grid.Get(x, y, z);
        if (!grid.Set(x, y, z, brick))
        {
            return;
        }

        //  TODO reimplement thrusters
        // if (currentBrick.Name == "thruster")
        // {
        //     _ecsContext.World.DataStore.Query<ThrusterComponent>(entity, 0f, ThrusterQuery);
        //     void ThrusterQuery(float delta, DataStore store, int thrusterEntity, ref ThrusterComponent thruster)
        //     {
        //         thruster.Power--;
        //
        //         if (thruster.Power <= 0)
        //         {
        //             store.Remove<ThrusterComponent>(thrusterEntity);
        //         }
        //     }
        // }
        //
        // if (brick.Name == "thruster")
        // {
        //     var updated = false;
        //     _ecsContext.World.DataStore.Query<ThrusterComponent>(entity, 0f, ThrusterQuery);
        //     void ThrusterQuery(float delta, DataStore store, int thrusterEntity, ref ThrusterComponent thruster)
        //     {
        //         thruster.Power++;
        //         updated = true;
        //     }
        //
        //     if (!updated)
        //     {
        //         _ecsContext.World.DataStore.AddOrUpdate(entity, new ThrusterComponent(power: 1));
        //     }
        // }
    }
}