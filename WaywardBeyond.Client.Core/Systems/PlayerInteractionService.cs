using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Reef;
using Shoal.Modularity;
using Swordfish.Audio;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Types;
using Swordfish.Library.Util;
using Swordfish.Physics;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Configuration;
using WaywardBeyond.Client.Core.Debug;
using WaywardBeyond.Client.Core.Items;
using WaywardBeyond.Client.Core.Numerics;
using WaywardBeyond.Client.Core.Player;
using WaywardBeyond.Client.Core.UI;
using WaywardBeyond.Client.Core.UI.Layers;
using WaywardBeyond.Client.Core.Voxels;
using WaywardBeyond.Client.Core.Voxels.Building;
using WaywardBeyond.Client.Core.Voxels.Models;

namespace WaywardBeyond.Client.Core.Systems;

using DebugInfo = (
    VoxelComponent VoxelComponent,
    Voxel Voxel,
    Int3 Coordinate,
    Vector3 Position,
    Vector3 Normal,
    float AlignmentCamera,
    float AlignmentSurface,
    bool AlignmentFloor
);

internal sealed class PlayerInteractionService : IEntryPoint, IDebugOverlay
{
    public readonly DataBinding<BrickShape> SelectedShape = new(BrickShape.Block);
    public readonly DataBinding<Orientation> SelectedOrientation = new();
    
    private static readonly Vector4 _gizmoColor = new(0.5f, 0.5f, 0.5f, 1f);
    private static readonly Vector3[] _worldAxes = [Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ];

    private readonly IInputService _inputService;
    private readonly IPhysics _physics;
    private readonly ILineRenderer _lineRenderer;
    private readonly IRenderContext _renderContext;
    private readonly IWindowContext _windowContext;
    private readonly VoxelEntityBuilder _voxelEntityBuilder;
    private readonly IECSContext _ecsContext;
    private readonly PlayerData _playerData;
    private readonly BrickDatabase _brickDatabase;
    private readonly ItemDatabase _itemDatabase;
    private readonly Dictionary<BrickShape, MeshGizmo> _shapeGizmos;
    private readonly Dictionary<Mesh, MeshGizmo> _meshGizmos = [];
    private MeshGizmo _activeGizmo;
    private readonly Line[] _debugLines;
    private readonly IAudioService _audioService;
    private readonly VolumeSettings _volumeSettings;
    private readonly DebugSettings _debugSettings;
    private readonly PlayerControllerSystem _playerControllerSystem;

    private readonly HashSet<InteractionBlocker> _interactionBlockers = [];
    
    private InteractionBlocker? _inputBlocker;
    private bool _snapPlacement;
    private DebugInfo _debugInfo;

    public PlayerInteractionService(
        in IInputService inputService,
        in IPhysics physics,
        in ILineRenderer lineRenderer,
        in IRenderContext renderContext,
        in IWindowContext windowContext,
        in VoxelEntityBuilder voxelEntityBuilder,
        in IECSContext ecsContext,
        in PlayerData playerData,
        in BrickDatabase brickDatabase,
        in ItemDatabase itemDatabase,
        in IAudioService audioService,
        in VolumeSettings volumeSettings,
        in DebugSettings debugSettings,
        in IAssetDatabase<Mesh> meshDatabase,
        in PlayerControllerSystem playerControllerSystem,
        in IShortcutService shortcutService
    ) {
        _inputService = inputService;
        _physics = physics;
        _lineRenderer = lineRenderer;
        _renderContext = renderContext;
        _windowContext = windowContext;
        _voxelEntityBuilder = voxelEntityBuilder;
        _ecsContext = ecsContext;
        _playerData = playerData;
        _brickDatabase = brickDatabase;
        _itemDatabase = itemDatabase;
        _audioService = audioService;
        _volumeSettings = volumeSettings;
        _debugSettings = debugSettings;
        _playerControllerSystem = playerControllerSystem;

        Mesh slope = meshDatabase.Get("slope.obj").Value;
        Mesh stair = meshDatabase.Get("stair.obj").Value;
        Mesh slab = meshDatabase.Get("slab.obj").Value;
        Mesh column = meshDatabase.Get("column.obj").Value;
        Mesh plate = meshDatabase.Get("plate.obj").Value;
        
        _shapeGizmos = new Dictionary<BrickShape, MeshGizmo>
        {
            { BrickShape.Any, new MeshGizmo(lineRenderer, _gizmoColor, new Cube())},
            { BrickShape.Custom, new MeshGizmo(lineRenderer, _gizmoColor, new Cube())},
            { BrickShape.Block, new MeshGizmo(lineRenderer, _gizmoColor, new Cube())},
            { BrickShape.Slab, new MeshGizmo(lineRenderer, _gizmoColor, slab)},
            { BrickShape.Stair, new MeshGizmo(lineRenderer, _gizmoColor, stair)},
            { BrickShape.Slope, new MeshGizmo(lineRenderer, _gizmoColor, slope)},
            { BrickShape.Column, new MeshGizmo(lineRenderer, _gizmoColor, column)},
            { BrickShape.Plate, new MeshGizmo(lineRenderer, _gizmoColor, plate)},
        };
        
        _activeGizmo = _shapeGizmos.Values.First();
        
        _debugLines = new Line[3];
        _debugLines[0] = lineRenderer.CreateLine(Vector3.Zero, Vector3.Zero, Vector4.One);
        _debugLines[1] = lineRenderer.CreateLine(Vector3.Zero, Vector3.Zero, Vector4.One);
        _debugLines[2] = lineRenderer.CreateLine(Vector3.Zero, Vector3.Zero, Vector4.One);
        
        WaywardBeyond.GameState.Changed += OnGameStateChanged;

        var snapModeShortcut = new Shortcut
        {
            Name = "Snap Placement",
            Category = "Interaction",
            Modifiers = ShortcutModifiers.None,
            Key = Key.Shift,
            IsEnabled = WaywardBeyond.IsPlaying,
            Action = OnSnapPlacementPressed,
            Released = OnSnapPlacementReleased,
        };
        shortcutService.RegisterShortcut(snapModeShortcut);
    }
    
    private void OnSnapPlacementPressed()
    {
        _snapPlacement = true;
    }
    
    private void OnSnapPlacementReleased()
    {
        _snapPlacement = false;
    }

    public void Run()
    {
        _physics.FixedUpdate += OnFixedUpdate;
        _inputService.Clicked += OnClicked;
    }
    
    public InteractionBlocker BlockInteraction()
    {
        return new InteractionBlocker(this, _playerControllerSystem);
    }

    public bool TryBlockInteractionExclusive([NotNullWhen(true)] out InteractionBlocker? interactionBlocker)
    {
        lock (_interactionBlockers)
        {
            if (_interactionBlockers.Count != 0)
            {
                interactionBlocker = null;
                return false;
            }

            interactionBlocker = BlockInteraction();
            return true;
        }
    }
    
    public bool IsInteractionBlocked()
    {
        lock (_interactionBlockers)
        {
            return _interactionBlockers.Count != 0;
        }
    }
    
    private void OnGameStateChanged(object? sender, DataChangedEventArgs<GameState> e)
    {
        SetInteractionEnabled(e.NewValue == GameState.Playing);
    }
    
    private void SetInteractionEnabled(bool enabled) 
    {
        lock (_interactionBlockers)
        {
            InteractionBlocker? blocker = _inputBlocker;
            blocker?.Dispose();
            _inputBlocker = enabled ? null : BlockInteraction();
        }
    }

    private void OnClicked(object? sender, ClickedEventArgs e)
    {
        if (WaywardBeyond.GameState != GameState.Playing || IsInteractionBlocked())
        {
            return;
        }

        Task.Run(() =>
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
        });
    }

    private void OnLeftClick()
    {
        if (!TryGetBrickFromScreenSpace(false, true, out Entity clickedEntity, out Voxel clickedVoxel, out Int3 brickPos, out VoxelComponent voxelComponent, out TransformComponent transformComponent))
        {
            return;
        }
        
        voxelComponent.VoxelObject.Set(brickPos.X, brickPos.Y, brickPos.Z, new Voxel());
        _voxelEntityBuilder.Rebuild(clickedEntity.Ptr);
        
        var soundIndex = Random.Shared.Next(1, 4);
        _audioService.Play($"sounds/metal_remove.{soundIndex}.wav", _volumeSettings.MixEffects());
        
        _ecsContext.World.DataStore.Query<PlayerComponent, InventoryComponent>(0f, PlayerInventoryQuery);
        return;

        void PlayerInventoryQuery(float delta, DataStore store, int playerEntity, ref PlayerComponent player, ref InventoryComponent inventory)
        {
            Result<BrickInfo> brickInfoResult = _brickDatabase.Get(clickedVoxel.ID);
            if (brickInfoResult.Success)
            {
                inventory.Add(new ItemStack(brickInfoResult.Value.ID));
            }
        }
    }

    private void OnRightClick()
    {
        if (!TryGetBrickFromScreenSpace(offset: true, reachAround: true, out Entity clickedEntity, out Voxel clickedVoxel, out Int3 brickPos, out VoxelComponent voxelComponent, out TransformComponent transformComponent, out Vector3 clickedPoint))
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
            BrickShape shape = brickInfo.Shapeable ? SelectedShape.Get() : brickInfo.Shape;
            
            //  If the selected shape is orientable for the brick, apply orientation.
            Vector3 worldPos = BrickToWorldSpace(brickPos, transformComponent.Position, transformComponent.Orientation);
            Orientation orientation = brickInfo.IsOrientable(shape) ? GetPlacementOrientation(transformComponent, clickedPoint, worldPos) : Orientation.Identity;

            var voxel = brickInfo.ToVoxel(shape, orientation);
            voxelComponent.VoxelObject.Set(brickPos.X, brickPos.Y, brickPos.Z, voxel);
            _voxelEntityBuilder.Rebuild(clickedEntity.Ptr);
            
            int soundIndex = Random.Shared.Next(1, 4);
            _audioService.Play($"sounds/metal_place.{soundIndex}.wav", _volumeSettings.MixEffects());
        }
    }
    
    private void OnMiddleClick()
    {
        if (!TryGetBrickFromScreenSpace(false, false, out Entity clickedEntity, out Voxel clickedVoxel, out Int3 brickPos, out VoxelComponent voxelComponent, out TransformComponent transformComponent))
        {
            return;
        }

        //  Clone the target brick's shape
        ShapeLight shapeLight = clickedVoxel.ShapeLight;
        SelectedShape.Set(shapeLight.Shape);
        
        //  If the player has a valid item, select it
        _ecsContext.World.DataStore.Query<PlayerComponent, InventoryComponent>(0f, PlayerInventoryQuery);
        void PlayerInventoryQuery(float delta, DataStore store, int playerEntity, ref PlayerComponent player, ref InventoryComponent inventory)
        {
            Result<BrickInfo> brickInfoResult = _brickDatabase.Get(clickedVoxel.ID);
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
                    continue;
                }

                if (itemResult.Value.Placeable == null)
                {
                    continue;
                }

                Result<BrickInfo> placeableBrickResult =  _brickDatabase.Get(itemResult.Value.Placeable.Value.ID);
                if (!placeableBrickResult.Success)
                {
                    continue;
                }

                if (placeableBrickResult.Value.DataID != clickedVoxel.ID)
                {
                    continue;
                }

                InventoryComponent playerInventory = inventory;
                store.Query<EquipmentComponent>(playerEntity, 0f, UpdateActiveSlotQuery);
                void UpdateActiveSlotQuery(float _, DataStore dataStore, int entity, ref EquipmentComponent equipment)
                {
                    if (i >= Hotbar.SLOT_COUNT)
                    {
                        playerInventory.Swap(equipment.ActiveInventorySlot, i);
                    }
                    else
                    {
                        equipment.ActiveInventorySlot = i;
                    }
                }
                break;
            }
        }
    }

    private void OnFixedUpdate(object? sender, EventArgs e)
    {
        if (WaywardBeyond.GameState < GameState.Playing)
        {
            return;
        }
        
        Result<BrickInfo> placeableResult = TryGetPlaceableBrickInfo();
        bool holdingPlaceable = placeableResult.Success;
        if (!TryGetBrickFromScreenSpace(holdingPlaceable, true, out Entity entity, out Voxel clickedVoxel, out Int3 brickPos, out VoxelComponent voxelComponent, out TransformComponent transformComponent, out Vector3 clickedPoint) 
            || !holdingPlaceable && clickedVoxel.ID == 0)
        {
            _debugInfo = default;
            _activeGizmo.Visible = false;
            _debugLines[0].Color = Vector4.Zero;
            _debugLines[1].Color = Vector4.Zero;
            _debugLines[2].Color = Vector4.Zero;
            return;
        }

        Vector3 worldPos = BrickToWorldSpace(brickPos, transformComponent.Position, transformComponent.Orientation);
        
        _debugInfo.VoxelComponent = voxelComponent;
        _debugInfo.Voxel = clickedVoxel;
        _debugInfo.Coordinate = brickPos;
        _debugInfo.Position = worldPos;
        
        Quaternion placeableOrientation = holdingPlaceable ? GetPlacementQuaternion(transformComponent, clickedPoint, worldPos) : transformComponent.Orientation * new Orientation(clickedVoxel.Orientation).ToQuaternion();

        //  TODO clean this up
        if (_debugSettings.OverlayVisible)
        {
            _debugLines[0].Color = new Vector4(1, 0, 0, 1);
            _debugLines[0].Start = worldPos;
            _debugLines[0].End = worldPos + Vector3.Transform(Vector3.UnitX, placeableOrientation);
            
            _debugLines[1].Color = new Vector4(0, 1, 0, 1);
            _debugLines[1].Start = worldPos;
            _debugLines[1].End = worldPos + Vector3.Transform(Vector3.UnitY, placeableOrientation);
            
            _debugLines[2].Color = new Vector4(0, 0, 1, 1);
            _debugLines[2].Start = worldPos;
            _debugLines[2].End = worldPos + Vector3.Transform(Vector3.UnitZ, placeableOrientation);
        }
        else
        {
            _debugLines[0].Color = Vector4.Zero;
            _debugLines[1].Color = Vector4.Zero;
            _debugLines[2].Color = Vector4.Zero;
        }

        BrickShape placeableShape;
        BrickInfo placeableBrickInfo;
        if (holdingPlaceable)
        {
            placeableBrickInfo = placeableResult.Value;
            placeableShape = placeableBrickInfo.Shapeable ? SelectedShape.Get() : placeableBrickInfo.Shape;
        }
        else
        {
            placeableResult = _brickDatabase.Get(clickedVoxel.ID);
            if (!placeableResult.Success)
            {
                _activeGizmo.Visible = false;
                return;
            }
            
            placeableBrickInfo = placeableResult.Value;
            placeableShape = new ShapeLight(clickedVoxel.ShapeLight).Shape;
        }
        
        if (placeableShape == BrickShape.Custom && placeableBrickInfo.Mesh != null)
        {
            if (!_meshGizmos.TryGetValue(placeableBrickInfo.Mesh, out MeshGizmo? meshGizmo))
            {
                meshGizmo = new MeshGizmo(_lineRenderer, _gizmoColor, placeableBrickInfo.Mesh);
                _meshGizmos.Add(placeableBrickInfo.Mesh, meshGizmo);
            }
            
            if (_activeGizmo != meshGizmo)
            {
                _activeGizmo.Visible = false;
                _activeGizmo = meshGizmo;
            }
        }
        else if (_shapeGizmos.TryGetValue(placeableShape, out MeshGizmo? meshGizmo) && _activeGizmo != meshGizmo)
        {
            _activeGizmo.Visible = false;
            _activeGizmo = meshGizmo;
        }
        
        _activeGizmo.Visible = true;
        _activeGizmo.Render(delta: 0.016f, new TransformComponent(worldPos, placeableOrientation, holdingPlaceable ? Vector3.One : new Vector3(1.0625f)));
    }
    
    private Quaternion GetPlacementQuaternion(TransformComponent transformComponent, Vector3 clickedPos, Vector3 brickPosWorld)
    {
        CameraEntity camera = _renderContext.MainCamera.Get();
        Quaternion baseQuaternion = GetPlacementLocalQuaternion(
            clickedPos, 
            brickPosWorld, 
            camera.Transform.Position, 
            camera.Transform.Orientation, 
            transformComponent.Orientation
        );

        Orientation selectedOrientation = SelectedOrientation.Get();
        var offsetQuaternion = selectedOrientation.ToQuaternion(); 
        Quaternion quaternion = baseQuaternion * offsetQuaternion;
        
        return transformComponent.Orientation * quaternion;
    }
    
    private Orientation GetPlacementOrientation(TransformComponent transformComponent, Vector3 clickedPos, Vector3 brickPosWorld)
    {
        Quaternion quaternion = GetPlacementQuaternion(transformComponent, clickedPos, brickPosWorld);
        return new Orientation(quaternion);
    }
    
    private Quaternion GetPlacementLocalQuaternion(
        Vector3 clickedPos, 
        Vector3 brickPosWorld, 
        Vector3 cameraPosition, 
        Quaternion cameraOrientation, 
        Quaternion gridOrientation
    ) {
        Vector3 brickToCameraNormal = Vector3.Normalize(cameraPosition - brickPosWorld);

        Vector3 cameraUpWorld = Vector3.Transform(Vector3.UnitY, cameraOrientation);
        Vector3 cameraForwardWorld = Vector3.Transform(-Vector3.UnitZ, cameraOrientation); 
        
        Vector3 clickedSurfaceNormal = Vector3.Normalize(brickPosWorld - clickedPos);
        float alignmentSurface = Vector3.Dot(clickedSurfaceNormal, cameraForwardWorld);
        _debugInfo.AlignmentSurface = alignmentSurface;
        
        Vector3 placementNormal;
        if (_snapPlacement)
        {
            placementNormal = clickedSurfaceNormal;
        }
        else 
        {
            placementNormal = brickToCameraNormal;
        }

        // Transform everything into the grid's local space
        Quaternion inverseGridRot = Quaternion.Inverse(gridOrientation);
        Vector3 normalLocal = Vector3.Transform(placementNormal, inverseGridRot);
        Vector3 cameraUpLocal = Vector3.Transform(cameraUpWorld, inverseGridRot);
        Vector3 cameraForwardLocal = Vector3.Transform(cameraForwardWorld, inverseGridRot);

        //  Snap the normal along the grid axes
        Vector3 snappedNormal = SnapToUnitAxis(normalLocal);
        
        // Determine if placement is targeting a "floor/ceiling" or "wall" relative to the camera
        float alignmentCamera = Vector3.Dot(placementNormal, cameraUpWorld);
        float absAlignment = Math.Abs(alignmentCamera);
        bool isPlacementOnFloor = absAlignment > 0.5f;

        Vector3 targetForward, targetUp;
        if (isPlacementOnFloor)
        {
            targetUp = snappedNormal;
            //  Project onto the camera's forward
            targetForward = GetMostPerpendicularAxis(targetUp, -cameraForwardLocal, cameraUpLocal);
        }
        else
        {
            targetForward = snappedNormal;
            //  Project onto the camera's up
            targetUp = GetMostPerpendicularAxis(targetForward, cameraUpLocal, cameraForwardLocal);
        }

        Vector3 targetRight = Vector3.Cross(targetUp, targetForward);

        var matrix = new Matrix4x4(
            targetRight.X,   targetRight.Y,   targetRight.Z,   0,
            targetUp.X,      targetUp.Y,      targetUp.Z,      0,
            targetForward.X, targetForward.Y, targetForward.Z, 0,
            0,               0,               0,               1
        );
        
        _debugInfo.Normal = snappedNormal;
        _debugInfo.AlignmentCamera = absAlignment;
        _debugInfo.AlignmentFloor = isPlacementOnFloor;
        return Quaternion.CreateFromRotationMatrix(matrix);
    }

    private static Vector3 SnapToUnitAxis(Vector3 vector)
    {
        float absX = Math.Abs(vector.X);
        float absY = Math.Abs(vector.Y);
        float absZ = Math.Abs(vector.Z);

        if (absX > absY && absX > absZ)
        {
            return new Vector3(Math.Sign(vector.X), 0, 0);
        }

        if (absY > absZ)
        {
            return new Vector3(0, Math.Sign(vector.Y), 0);
        }

        return new Vector3(0, 0, Math.Sign(vector.Z));
    }
    
    /// <summary>
    ///     Gets the axis that is most perpendicular to the provided
    ///     vector when projected onto a reference vector.
    /// </summary>
    private static Vector3 GetMostPerpendicularAxis(Vector3 vector, Vector3 preferredReference, Vector3 fallbackReference)
    {
        Vector3 bestUp = Vector3.Zero;
        var maxDot = float.MinValue;

        //  Prefer up, but fallback to forward if up is parallel to the vector
        Vector3 reference;
        if (Math.Abs(Vector3.Dot(vector, preferredReference)) <= 0.95f)
        {
            reference = preferredReference;
        }
        else
        {
            reference = fallbackReference;
        }

        //  Find the axis that is most perpendicular with the reference vector
        for (var i = 0; i < _worldAxes.Length; i++)
        {
            Vector3 axis = _worldAxes[i];
            
            // Skip any axis that is not (roughly) perpendicular
            if (Math.Abs(Vector3.Dot(axis, vector)) > 0.01f)
            {
                continue;
            }

            float dot = Vector3.Dot(axis, reference);
            if (dot <= maxDot)
            {
                continue;
            }

            maxDot = dot;
            bestUp = axis;
        }

        return bestUp;
    }

    public Result RenderDebugOverlay(double delta, UIBuilder<Material> ui)
    {
        DebugInfo debugInfo = _debugInfo;

        Result<BrickInfo> brickInfoResult = _brickDatabase.Get(debugInfo.Voxel.ID);
        string brickID = brickInfoResult.Success ? brickInfoResult.Value.ID : "UNKNOWN";
        
        using (ui.Text($"Voxel: {debugInfo.Voxel.ID} ({brickID})")) {}
        using (ui.Text($"Coordinate: {debugInfo.Coordinate}")) {}
        using (ui.Text($"Position: {debugInfo.Position:N3}")) {}
        using (ui.Text($"Normal: {debugInfo.Normal:N0}")) {}
        using (ui.Text($"Align (Cam): {debugInfo.AlignmentCamera:N3}")) {}
        using (ui.Text($"Align (Sur): {debugInfo.AlignmentSurface:N3}")) {}
        using (ui.Text($"Align (Vert): {debugInfo.AlignmentFloor}")) {}
        using (ui.Text($"Snap: {_snapPlacement}")) {}
        
        return Result.FromSuccess();
    }

    private bool TryGetBrickFromScreenSpace(
        bool offset,
        bool reachAround,
        out Entity entity,
        out Voxel voxel,
        out Int3 coordinate,
        out VoxelComponent voxelComponent,
        out TransformComponent transformComponent
    ) {
        return TryGetBrickFromScreenSpace(
            offset,
            reachAround,
            out entity,
            out voxel,
            out coordinate,
            out voxelComponent,
            out transformComponent,
            out _
        );
    }
    
    private bool TryGetBrickFromScreenSpace(
        bool offset,
        bool reachAround,
        out Entity entity,
        out Voxel voxel,
        out Int3 coordinate,
        out VoxelComponent voxelComponent,
        out TransformComponent transformComponent,
        out Vector3 clickedPoint
    ) {
        CameraEntity cameraEntity = _renderContext.MainCamera.Get();

        Vector3? reachAroundDir = null;
        Ray ray = cameraEntity.ScreenPointToRay((int)_windowContext.Resolution.X / 2, (int)_windowContext.Resolution.Y / 2, (int)_windowContext.Resolution.X, (int)_windowContext.Resolution.Y);
        //  TODO #319 offset origin by the player's collider without hardcoded value or allow raycasting against a layer mask
        ray = new Ray(ray.Origin + new Vector3(0.26f) * ray.Vector, ray.Vector * 9.5f);
        if (!TryRaycastBrickEntity(ray, out RaycastResult raycast, out voxelComponent, out transformComponent))
        {
            if (!reachAround || !TryReachAroundRaycasts(ray, cameraEntity, ref reachAroundDir, out voxelComponent, out transformComponent, out raycast))
            {
                entity = default;
                voxel = default;
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
        voxel = voxelComponent.VoxelObject.Get(coordinate.X, coordinate.Y, coordinate.Z);
        
        if (reachAroundDir != null)
        {
            TryGetRelativeBrickInWorldSpace(voxelComponent, transformComponent, reachAroundDir.Value, ref coordinate, out voxel);
        }
        
        entity = raycast.Entity;
        return true;
    }

    private bool TryReachAroundRaycasts(
        Ray centerRay,
        CameraEntity cameraEntity,
        ref Vector3? reachAroundDir,
        out VoxelComponent voxelComponent,
        out TransformComponent transformComponent,
        out RaycastResult raycast
    ) {
        const float reachAroundWidth = 0.5f;
        
        if (TryReachAroundRaycast(centerRay * 0.9f, direction: cameraEntity.Transform.GetUp(), reachAroundWidth, ref reachAroundDir, out voxelComponent, out transformComponent, out raycast))
        {
            return true;
        }

        if (TryReachAroundRaycast(centerRay * 0.9f, direction: cameraEntity.Transform.GetRight(), reachAroundWidth, ref reachAroundDir, out voxelComponent, out transformComponent, out raycast))
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
        out VoxelComponent voxelComponent,
        out TransformComponent transformComponent,
        out RaycastResult raycast
    ) {
        var ray = new Ray(centerRay.Origin + direction * reachAroundWidth, centerRay.Vector);
        if (TryRaycastBrickEntity(ray, out raycast, out voxelComponent, out transformComponent))
        {
            reachAroundDir = -direction;
            return true;
        }

        ray = new Ray(centerRay.Origin + direction * -reachAroundWidth, centerRay.Vector);
        if (TryRaycastBrickEntity(ray, out raycast, out voxelComponent, out transformComponent))
        {
            reachAroundDir = direction;
            return true;
        }

        return false;
    }

    private static void TryGetRelativeBrickInWorldSpace(
        VoxelComponent voxelComponent,
        TransformComponent transformComponent,
        Vector3 worldNormal,
        ref Int3 coordinate,
        out Voxel voxel
    ) {
        Vector3 worldPos = BrickToWorldSpace(coordinate, transformComponent.Position, transformComponent.Orientation);

        worldNormal = new Vector3(
            (float)Math.Round(worldNormal.X, MidpointRounding.AwayFromZero),
            (float)Math.Round(worldNormal.Y, MidpointRounding.AwayFromZero),
            (float)Math.Round(worldNormal.Z, MidpointRounding.AwayFromZero)
        );
        
        worldPos += worldNormal;
        coordinate = WorldToBrickSpace(worldPos, transformComponent.Position, transformComponent.Orientation);
        voxel = voxelComponent.VoxelObject.Get(coordinate.X, coordinate.Y, coordinate.Z);
    }

    private bool TryRaycastBrickEntity(Ray ray, out RaycastResult raycast, out VoxelComponent voxelComponent, out TransformComponent transformComponent)
    {
        raycast = _physics.Raycast(ray);
        if (raycast.Hit && raycast.Entity.TryGet(out voxelComponent) && raycast.Entity.TryGet(out transformComponent))
        {
            return true;
        }

        voxelComponent = default;
        transformComponent = default;
        return false;
    }

    private static Int3 WorldToBrickSpace(Vector3 position, Vector3 origin, Quaternion orientation)
    {
        Vector3 localPos = Vector3.Transform(position - origin, Quaternion.Inverse(orientation)) + new Vector3(0.5f);
        
        var x = (int)Math.Floor(localPos.X);
        var y = (int)Math.Floor(localPos.Y);
        var z = (int)Math.Floor(localPos.Z);

        return new Int3(x, y, z);
    }

    private static Vector3 BrickToWorldSpace(Int3 coordinate, Vector3 origin, Quaternion orientation)
    {
        var localCenter = new Vector3(
            coordinate.X,
            coordinate.Y,
            coordinate.Z
        );

        return Vector3.Transform(localCenter, orientation) + origin;
    }
    
    private Result<BrickInfo> TryGetPlaceableBrickInfo()
    {
        Result<ItemSlot> mainHandResult = _playerData.GetMainHand(_ecsContext.World.DataStore);
        if (!mainHandResult.Success || mainHandResult.Value.Item.Placeable == null)
        {
            return new Result<BrickInfo>(success: false, null!, mainHandResult.Message, mainHandResult.Exception);
        }
        
        ItemSlot mainHand = mainHandResult.Value;
        Item item = mainHand.Item;
        PlaceableDefinition placeable = item.Placeable.Value;

        if (placeable.Type != PlaceableType.Brick)
        {
            return new Result<BrickInfo>(success: false, null!);
        }

        Result<BrickInfo> brickInfoResult = _brickDatabase.Get(placeable.ID);
        if (!brickInfoResult.Success)
        {
            return new Result<BrickInfo>(success: false, null!, brickInfoResult.Message, brickInfoResult.Exception);
        }
        
        return Result<BrickInfo>.FromSuccess(brickInfoResult.Value);
    }
    
    public sealed class InteractionBlocker : IDisposable
    {
        private readonly PlayerInteractionService _playerInteractionService;
        private readonly PlayerControllerSystem _playerControllerSystem;

        internal InteractionBlocker(in PlayerInteractionService playerInteractionService, in PlayerControllerSystem playerControllerSystem)
        {
            _playerInteractionService = playerInteractionService;
            _playerControllerSystem = playerControllerSystem;
            lock (playerInteractionService._interactionBlockers)
            {
                playerInteractionService._interactionBlockers.Add(this);
                playerControllerSystem.SetInputEnabled(false);
            }
        }

        public void Dispose()
        {
            lock (_playerInteractionService._interactionBlockers)
            {
                _playerInteractionService._interactionBlockers.Remove(this);
                _playerControllerSystem.SetInputEnabled(!_playerInteractionService.IsInteractionBlocked());
            }
        }
    }
}