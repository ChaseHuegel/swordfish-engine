using System;
using System.Numerics;
using Reef;
using Shoal.Modularity;
using Swordfish.Bricks;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using Swordfish.Physics;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Debug;
using WaywardBeyond.Client.Core.Items;
using WaywardBeyond.Client.Core.Player;
using WaywardBeyond.Client.Core.UI;

namespace WaywardBeyond.Client.Core.Systems;

internal sealed class PlayerInteractionService : IEntryPoint, IDebugOverlay
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

    private (BrickComponent BrickComponent, Brick Brick, (int X, int Y, int Z) Coordinate, Vector3 Position) _debugInfo;

    public PlayerInteractionService(
        in IInputService inputService,
        in IPhysics physics,
        in ILineRenderer lineRenderer,
        in IRenderContext renderContext,
        in IWindowContext windowContext,
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
    }

    public void Run()
    {
        _physics.FixedUpdate += OnFixedUpdate;
        _inputService.Clicked += OnClicked;
    }

    private void OnClicked(object? sender, ClickedEventArgs e)
    {
        if (WaywardBeyond.GameState != GameState.Playing)
        {
            return;
        }
        
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
        if (!TryGetBrickFromScreenSpace(false, true, out Entity clickedEntity, out Brick clickedBrick, out (int X, int Y, int Z) brickPos, out BrickComponent brickComponent, out TransformComponent transformComponent))
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
        if (!TryGetBrickFromScreenSpace(offset: true, reachAround: true, out Entity clickedEntity, out Brick clickedBrick, out (int X, int Y, int Z) brickPos, out BrickComponent brickComponent, out TransformComponent transformComponent, out Vector3 clickedPoint))
        {
            return;
        }

        _ecsContext.World.DataStore.Query<PlayerComponent, InventoryComponent>(0f, TryConsumeItemQuery);
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
            
            //  If this brick is shapeable, use the selected shape.
            BrickShape shape = brickInfo.Shapeable ? _shapeSelector.SelectedShape.Get() : brickInfo.Shape;
            
            //  If the selected shape is orientable for the brick, apply orientation.
            BrickOrientation orientation = BrickOrientation.Identity;
            if (brickInfo.IsOrientable(shape))
            {
                //  Base off the selected orientation
                orientation = _orientationSelector.SelectedOrientation.Get();

                //  Apply pitch and yaw to look toward the camera
                Camera camera = _renderContext.Camera.Get();
                Vector2 lookAt = LookAtEuler(clickedPoint, transformComponent.Orientation, camera.Transform.Position);
                orientation.PitchRotations -= (int)Math.Round(lookAt.X / 90, MidpointRounding.ToEven);
                orientation.YawRotations += (int)Math.Round(lookAt.Y / 90, MidpointRounding.ToEven);
            }

            var brick = brickInfo.ToBrick();
            brick.Data = (byte)shape;
            brick.Orientation = orientation;
            
            SetBrick(clickedEntity.Ptr, brickComponent.Grid, brickPos.X, brickPos.Y, brickPos.Z, brick);
            _brickEntityBuilder.Rebuild(clickedEntity.Ptr);
        }
    }
    
    private static Vector2 LookAtEuler(Vector3 model, Quaternion orientation, Vector3 view)
    {
        Vector3 worldDir = Vector3.Normalize(view - model);
    
        Quaternion invOrientation = Quaternion.Inverse(orientation);
        Vector3 localDir = Vector3.Transform(worldDir, invOrientation);
    
        float yaw = MathF.Atan2(localDir.X, localDir.Z);
        float pitch = MathF.Atan2(localDir.Y, MathF.Sqrt(localDir.X * localDir.X + localDir.Z * localDir.Z));
    
        return new Vector2(pitch * (180f / MathF.PI), yaw * (180f / MathF.PI));
    }
    
    private void OnMiddleClick()
    {
        if (!TryGetBrickFromScreenSpace(false, false, out Entity clickedEntity, out Brick clickedBrick, out (int X, int Y, int Z) brickPos, out BrickComponent brickComponent, out TransformComponent transformComponent))
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
        if (!TryGetBrickFromScreenSpace(true, true, out Entity entity, out Brick clickedBrick, out (int X, int Y, int Z) brickPos, out BrickComponent brickComponent, out TransformComponent transformComponent))
        {
            _cubeGizmo.Visible = false;
            return;
        }
        
        Vector3 worldPos = BrickToWorldSpace(brickPos, transformComponent.Position, transformComponent.Orientation);
        _cubeGizmo.Visible = true;
        _cubeGizmo.Render(new TransformComponent(worldPos, transformComponent.Orientation));

        _debugInfo = (BrickComponent: brickComponent, Brick: clickedBrick, Coordinate: brickPos, Position: worldPos);
    }
    
    public Result RenderDebugOverlay(double delta, UIBuilder<Material> ui)
    {
        (BrickComponent BrickComponent, Brick Brick, (int X, int Y, int Z) Coordinate, Vector3 Position) debugInfo = _debugInfo;
        
        using (ui.Text($"Center of mass: {debugInfo.BrickComponent.Grid.CenterOfMass}")) {}
        using (ui.Text($"Brick: {debugInfo.Brick}")) {}
        using (ui.Text($"Coordinate: {debugInfo.Coordinate}")) {}
        using (ui.Text($"Position: {debugInfo.Position}")) {}
        
        return Result.FromSuccess();
    }

    private bool TryGetBrickFromScreenSpace(
        bool offset,
        bool reachAround,
        out Entity entity,
        out Brick brick,
        out (int X, int Y, int Z) coordinate,
        out BrickComponent brickComponent,
        out TransformComponent transformComponent
    ) {
        return TryGetBrickFromScreenSpace(
            offset,
            reachAround,
            out entity,
            out brick,
            out coordinate,
            out brickComponent,
            out transformComponent,
            out _
        );
    }
    
    private bool TryGetBrickFromScreenSpace(
        bool offset,
        bool reachAround,
        out Entity entity,
        out Brick brick,
        out (int X, int Y, int Z) coordinate,
        out BrickComponent brickComponent,
        out TransformComponent transformComponent,
        out Vector3 clickedPoint
    ) {
        Camera camera = _renderContext.Camera.Get();
        Vector2 cursorPos = _inputService.CursorPosition;

        Vector3? reachAroundDir = null;
        Ray ray = camera.ScreenPointToRay((int)cursorPos.X, (int)cursorPos.Y, (int)_windowContext.Resolution.X, (int)_windowContext.Resolution.Y);
        if (!TryRaycastBrickEntity(ray, out RaycastResult raycast, out brickComponent, out transformComponent))
        {
            if (!reachAround || !TryReachAroundRaycasts(ray, camera, ref reachAroundDir, out brickComponent, out transformComponent, out raycast))
            {
                entity = default;
                brick = default;
                coordinate = default;
                clickedPoint = default;
                return false;
            }
        }
        
        clickedPoint = raycast.Point;
        Vector3 worldPos = raycast.Point;
        if (offset && reachAroundDir == null)
        {
            worldPos += raycast.Normal * 0.1f;
        }
        else
        {
            worldPos += raycast.Normal * -0.1f;
        }
        
        coordinate = WorldToBrickSpace(worldPos, transformComponent.Position, transformComponent.Orientation);
        brick = brickComponent.Grid.Get(coordinate.X, coordinate.Y, coordinate.Z);
        
        if (reachAroundDir != null)
        {
            TryGetRelativeBrickInWorldSpace(brickComponent, transformComponent, reachAroundDir.Value, ref coordinate, out brick);
        }
        
        entity = raycast.Entity;
        return true;
    }

    private bool TryReachAroundRaycasts(
        Ray centerRay,
        Camera camera,
        ref Vector3? reachAroundDir,
        out BrickComponent brickComponent,
        out TransformComponent transformComponent,
        out RaycastResult raycast
    ) {
        const float reachAroundWidth = 0.5f;
        
        if (TryReachAroundRaycast(centerRay, direction: camera.Transform.GetUp(), reachAroundWidth, ref reachAroundDir, out brickComponent, out transformComponent, out raycast))
        {
            return true;
        }

        if (TryReachAroundRaycast(centerRay, direction: camera.Transform.GetRight(), reachAroundWidth, ref reachAroundDir, out brickComponent, out transformComponent, out raycast))
        {
            return true;
        }

        return false;
    }

    private bool TryReachAroundRaycast(
        Ray centerRay,
        Vector3 direction,
        float reachAroundWidth,
        ref Vector3? reachAroundDir,
        out BrickComponent brickComponent,
        out TransformComponent transformComponent,
        out RaycastResult raycast
    ) {
        var ray = new Ray(centerRay.Origin + direction * reachAroundWidth, centerRay.Vector);
        if (TryRaycastBrickEntity(ray, out raycast, out brickComponent, out transformComponent))
        {
            reachAroundDir = -direction;
            return true;
        }

        ray = new Ray(centerRay.Origin + direction * -reachAroundWidth, centerRay.Vector);
        if (TryRaycastBrickEntity(ray, out raycast, out brickComponent, out transformComponent))
        {
            reachAroundDir = direction;
            return true;
        }

        return false;
    }

    private static void TryGetRelativeBrickInWorldSpace(
        BrickComponent brickComponent,
        TransformComponent transformComponent,
        Vector3 worldNormal,
        ref (int X, int Y, int Z) coordinate,
        out Brick brick
    ) {
        Vector3 worldPos = BrickToWorldSpace(coordinate, transformComponent.Position, transformComponent.Orientation);

        worldNormal = new Vector3(
            (float)Math.Round(worldNormal.X, MidpointRounding.AwayFromZero),
            (float)Math.Round(worldNormal.Y, MidpointRounding.AwayFromZero),
            (float)Math.Round(worldNormal.Z, MidpointRounding.AwayFromZero)
        );
        
        worldPos += worldNormal;
        coordinate = WorldToBrickSpace(worldPos, transformComponent.Position, transformComponent.Orientation);
        brick = brickComponent.Grid.Get(coordinate.X, coordinate.Y, coordinate.Z);
    }

    private bool TryRaycastBrickEntity(Ray ray, out RaycastResult raycast, out BrickComponent brickComponent, out TransformComponent transformComponent)
    {
        raycast = _physics.Raycast(ray * 1000);
        if (raycast.Hit && raycast.Entity.TryGet(out brickComponent) && raycast.Entity.TryGet(out transformComponent))
        {
            return true;
        }

        brickComponent = default;
        transformComponent = default;
        return false;
    }

    private static (int X, int Y, int Z) WorldToBrickSpace(Vector3 position, Vector3 origin, Quaternion orientation)
    {
        Vector3 localPos = Vector3.Transform(position - origin, Quaternion.Inverse(orientation)) + new Vector3(0.5f);
        
        var x = (int)Math.Floor(localPos.X);
        var y = (int)Math.Floor(localPos.Y);
        var z = (int)Math.Floor(localPos.Z);

        return (x, y, z);
    }

    private static Vector3 BrickToWorldSpace((int X, int Y, int Z) coordinate, Vector3 origin, Quaternion orientation)
    {
        var localCenter = new Vector3(
            coordinate.X,
            coordinate.Y,
            coordinate.Z
        );

        return Vector3.Transform(localCenter, orientation) + origin;
    }
    
    private void SetBrick(int _, BrickGrid grid, int x, int y, int z, Brick brick)
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