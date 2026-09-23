using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Reef;
using Shoal.Modularity;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Types;
using Swordfish.Library.Util;
using Swordfish.Physics;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Networking;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Configuration;
using WaywardBeyond.Client.Core.Debug;
using WaywardBeyond.Client.Core.Events;
using WaywardBeyond.Client.Core.Items;
using WaywardBeyond.Client.Core.Numerics;
using WaywardBeyond.Client.Core.Player;
using WaywardBeyond.Client.Core.Services;
using WaywardBeyond.Client.Core.UI;
using WaywardBeyond.Client.Core.UI.Layers;
using WaywardBeyond.Client.Core.Voxels.Building;
using WaywardBeyond.Client.Core.Voxels.Models;
using WaywardBeyond.Client.Core.Voxels;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Gameplay;
using WaywardBeyond.Shared.Networking.Components;

namespace WaywardBeyond.Client.Core.Systems;

using DebugInfo = (
    VoxelComponent VoxelComponent,
    Shared.Data.Voxel Voxel,
    Int3 Coordinate,
    Vector3 Position,
    Vector3 Normal,
    float AlignmentCamera,
    float AlignmentSurface,
    bool AlignmentFloor
);

internal sealed class PlayerInteractionService : IEntryPoint, IEntitySystem, IDebugOverlay
{
    private static readonly Vector4 _gizmoColor = new(0.5f, 0.5f, 0.5f, 1f);
    private static readonly Vector3[] _worldAxes = [Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ];

    private readonly InteractionState _interactionState;
    private readonly IInputService _inputService;
    private readonly IPhysics _physics;
    private readonly ILineRenderer _lineRenderer;
    private readonly IRenderContext _renderContext;
    private readonly IWindowContext _windowContext;
    private readonly PlayerData _playerData;
    private readonly BrickDatabase _brickDatabase;
    private readonly ItemDatabase _itemDatabase;
    private readonly Dictionary<BrickShape, MeshGizmo> _shapeGizmos;
    private readonly Dictionary<Mesh, MeshGizmo> _meshGizmos = [];
    private MeshGizmo _activeGizmo;
    private readonly Line[] _debugLines;
    private readonly DebugSettings _debugSettings;
    private readonly SoundEffectService _soundEffectService;
    private readonly EventInvoker<PlaceEvent> _placeEvent;
    private readonly EventInvoker<BreakEvent> _breakEvent;
    private readonly IInteractionContent _content;
    private readonly SnapshotAckTracker _snapshotAck;

    private uint _interactionSequence;

    //  Click edges are pushed here from the window/input thread and drained on the ECS thread in Tick,
    //  because every interaction path (resolution + store mutation + VoxelObject/Rebuild + SFX hooks)
    //  must run on the ECS thread, never off-thread.
    private readonly ConcurrentQueue<MouseButton> _pendingClicks = new();

    //  The client world store, captured on the ECS thread at the first tick. Mutations and queries must
    //  not reach back through IECSContext (that would reintroduce a container cycle now that this type
    //  is itself an IEntitySystem), so Tick stashes the store it is handed.
    private DataStore? _store;

    private DebugInfo _debugInfo;

    public PlayerInteractionService(
        in InteractionState interactionState,
        in IInputService inputService,
        in IPhysics physics,
        in ILineRenderer lineRenderer,
        in IRenderContext renderContext,
        in IWindowContext windowContext,
        in PlayerData playerData,
        in BrickDatabase brickDatabase,
        in ItemDatabase itemDatabase,
        in DebugSettings debugSettings,
        in IAssetDatabase<Mesh> meshDatabase,
        in IShortcutService shortcutService,
        in SoundEffectService soundEffectService,
        in EventInvoker<PlaceEvent> placeEvent,
        in EventInvoker<BreakEvent> breakEvent,
        in IInteractionContent content,
        in SnapshotAckTracker snapshotAck
    ) {
        _interactionState = interactionState;
        _inputService = inputService;
        _physics = physics;
        _lineRenderer = lineRenderer;
        _renderContext = renderContext;
        _windowContext = windowContext;
        _playerData = playerData;
        _brickDatabase = brickDatabase;
        _itemDatabase = itemDatabase;
        _debugSettings = debugSettings;
        _soundEffectService = soundEffectService;
        _placeEvent = placeEvent;
        _breakEvent = breakEvent;
        _content = content;
        _snapshotAck = snapshotAck;

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
        _interactionState.SnapPlacement.Set(true);
    }
    
    private void OnSnapPlacementReleased()
    {
        _interactionState.SnapPlacement.Set(false);
    }

    public void Run()
    {
        _physics.FixedUpdate += OnFixedUpdate;
        _inputService.Clicked += OnClicked;
    }
    
    private void OnGameStateChanged(object? sender, DataChangedEventArgs<GameState> e)
    {
        _interactionState.SetInteractionEnabled(e.NewValue == GameState.Playing);
    }

    private void OnClicked(object? sender, ClickedEventArgs e)
    {
        if (WaywardBeyond.GameState != GameState.Playing || _interactionState.IsInteractionBlocked())
        {
            return;
        }

        _pendingClicks.Enqueue(e.MouseButton);
    }

    /// <summary>
    /// Drains click edges on the ECS thread. The interaction resolution and its store mutations, voxel
    /// prediction/rebuild, and presentation hooks all mutate ECS-owned state, so they must never run on
    /// the window/input thread.
    /// </summary>
    public void Tick(float delta, DataStore store)
    {
        _store = store;
        while (_pendingClicks.TryDequeue(out MouseButton button))
        {
            DispatchButton(button);
        }
    }

    private void DispatchButton(MouseButton button)
    {
        if (button == MouseButton.Left)
        {
            OnLeftClick();
        }

        if (button == MouseButton.Right)
        {
            OnRightClick();
        }

        if (button == MouseButton.Middle)
        {
            OnMiddleClick();
        }
    }

    private void OnLeftClick()
    {
        AttemptVoxelInteraction(InteractionKind.PrimaryPressed);
    }

    private void OnRightClick()
    {
        AttemptVoxelInteraction(InteractionKind.SecondaryPressed);
    }

    /// <summary>
    /// Refactors the old client-authoritative interaction into intent + prediction: the client derives the
    /// target cell with the same shared resolver the server uses, predicts the outcome onto its
    /// presentation-only voxel container, records the prediction for reconcile, and sends the edge event
    /// (with its hint) upstream for the server to validate and author. The server owns inventory
    /// consumption/loot, so nothing here mutates the client inventory.
    /// </summary>
    private void AttemptVoxelInteraction(InteractionKind kind)
    {
        if (WaywardBeyond.GameState != GameState.Playing || _interactionState.IsInteractionBlocked())
        {
            return;
        }

        DataStore store = _store ?? throw new InvalidOperationException("Interaction attempted before the ECS store was available.");
        bool isPlace = kind == InteractionKind.SecondaryPressed;

        int playerEntity = -1;
        store.Query<PlayerComponent>(0f, (float _, DataStore s, int e, in PlayerComponent playerComponent) => playerEntity = e);
        if (playerEntity < 0)
        {
            return;
        }

        GameMode mode = GameMode.Creative;
        if (store.TryGet(playerEntity, out GameModeComponent gameModeComponent))
        {
            mode = gameModeComponent.Mode;
        }

        string? heldItemID = GetHeldItemID(store, playerEntity);
        PlaceableBrick? placeable = null;
        if (isPlace && heldItemID != null && _content.TryGetPlaceable(heldItemID, out PlaceableBrick contentPlaceable))
        {
            placeable = contentPlaceable;
        }

        if (!TryBuildCenterRay(out Ray centerRay))
        {
            return;
        }

        var world = new ClientVoxelInteractionWorld(store, _physics);
        if (!SharedInteractionResolver.TryResolveTargetCell(centerRay, offset: isPlace, reachAround: true, SharedInteractionResolver.DEFAULT_REACH, world, out Int3 coordinate))
        {
            return;
        }

        BrickInteraction hint = BuildInteractionHint(isPlace, in placeable, centerRay, store, coordinate);

        InteractionResolution resolution = SharedInteractionResolver.Resolve(centerRay, hint, kind, placeable, mode, SharedInteractionResolver.DEFAULT_REACH, world);
        if (resolution.Action == InteractionAction.None)
        {
            return;
        }

        //  Presentation hooks (ghost/SFX) fire before the prediction; a cancel skips the local prediction
        //  (the server still validates the sent intent). Unconsumed inventory is the server's job.
        if (!FirePresentationHook(in resolution, kind))
        {
            return;
        }

        ApplyVoxelPrediction(store, playerEntity, kind, in resolution, in hint);
    }

    private bool TryBuildCenterRay(out Ray centerRay)
    {
        CameraEntity cameraEntity = _renderContext.MainCamera.Get();
        centerRay = cameraEntity.ScreenPointToRay((int)_windowContext.Resolution.X / 2, (int)_windowContext.Resolution.Y / 2, (int)_windowContext.Resolution.X, (int)_windowContext.Resolution.Y);
        return true;
    }

    private BrickInteraction BuildInteractionHint(bool isPlace, in PlaceableBrick? placeable, in Ray ray, DataStore store, Int3 coordinate)
    {
        byte hintShape = 0;
        byte hintOrientation = 0;

        if (isPlace && placeable != null)
        {
            BrickShape shape = placeable.Value.Shapeable ? _interactionState.SelectedShape.Get() : placeable.Value.Shape;
            hintShape = (byte)shape;
            hintOrientation = TryResolvePlacementOrientation(store, ray, coordinate);
        }

        return new BrickInteraction
        {
            TargetX = coordinate.X,
            TargetY = coordinate.Y,
            TargetZ = coordinate.Z,
            HintShape = hintShape,
            HintOrientation = hintOrientation,
        };
    }

    private byte TryResolvePlacementOrientation(DataStore store, in Ray ray, Int3 brickCoordinate)
    {
        RaycastResult raycast = _physics.Raycast(ray);
        if (!raycast.Hit || !store.TryGet(raycast.Entity.Ptr, out TransformComponent transform))
        {
            return 0;
        }

        Vector3 worldPos = SharedInteractionResolver.BrickToWorldSpace(brickCoordinate, transform.Position, transform.Orientation);
        Orientation orientation = GetPlacementLocalOrientation(transform, raycast.Point, worldPos);
        return orientation.ToByte();
    }

    private bool FirePresentationHook(in InteractionResolution resolution, InteractionKind kind)
    {
        Result<BrickInfo> brickInfoResult = _brickDatabase.Get(resolution.Voxel.ID);
        if (!brickInfoResult.Success)
        {
            return true;
        }

        if (kind == InteractionKind.PrimaryPressed)
        {
            if (!_breakEvent.Invoke(new BreakEvent(brickInfoResult.Value)).Success)
            {
                return false;
            }

            if (brickInfoResult.Value.Tags.Contains("environment"))
            {
                _soundEffectService.PlayRemoveRock();
            }
            else
            {
                _soundEffectService.PlayRemoveMetal();
            }
        }
        else
        {
            if (!_placeEvent.Invoke(new PlaceEvent(brickInfoResult.Value)).Success)
            {
                return false;
            }

            if (brickInfoResult.Value.Tags.Contains("environment"))
            {
                _soundEffectService.PlayPlaceRock();
            }
            else
            {
                _soundEffectService.PlayPlaceMetal();
            }
        }

        return true;
    }

    private void ApplyVoxelPrediction(DataStore store, int playerEntity, InteractionKind kind, in InteractionResolution resolution, in BrickInteraction hint)
    {
        if (!store.TryGet(resolution.Entity, out VoxelComponent voxelComponent))
        {
            return;
        }

        VoxelObject voxelObject = voxelComponent.VoxelObject;
        Int3 coordinate = resolution.Coordinate;
        Voxel original = voxelObject.Get(coordinate.X, coordinate.Y, coordinate.Z);

        //  Breaking writes an empty voxel; placing writes the resolved placed brick - the same voxel the
        //  server authoritatively writes, so a confirm-match reconciles to a no-op.
        Voxel predicted = kind == InteractionKind.PrimaryPressed ? new Voxel() : resolution.Voxel;

        //  Predict the authoritative outcome onto the presentation-only voxel container. The reconcile
        //  system confirms, snaps, or reverts this against the server's authoritative VoxelEditMessage.
        voxelObject.Set(coordinate.X, coordinate.Y, coordinate.Z, predicted);

        //  Publish the edit by marking the voxel component dirty; the VoxelEntityRebuildSystem observes
        //  this flag and fulfills the mesh/collider rebuild on the ECS thread.
        store.MarkDirty<VoxelComponent>(resolution.Entity);

        PendingInteractionComponent pending = GetOrCreatePending(store, playerEntity);
        uint sequence = ++_interactionSequence;
        uint serverTickAtSample = _snapshotAck.LastAppliedSnapshotTick;
        pending.Queue.Register(resolution.Entity, coordinate, original, predicted, sequence, serverTickAtSample);

        var interaction = new InteractionEvent
        {
            Entity = store.GetUuid(playerEntity).ToValue(),
            SequenceNumber = sequence,
            ServerTickAtSample = serverTickAtSample,
            Kind = (byte)kind,
            Brick = hint,
        };

        //  Buffer the discrete edge for replication rather than overwriting a single InteractionEvent
        //  component slot - every click is retained, so rapid placements between sends are not dropped.
        pending.Outbound.Stage(interaction);

        store.AddOrUpdate(playerEntity, pending);
    }

    private static PendingInteractionComponent GetOrCreatePending(DataStore store, int playerEntity)
    {
        if (store.TryGet(playerEntity, out PendingInteractionComponent existing) && existing.Queue != null)
        {
            return existing;
        }

        return new PendingInteractionComponent(new PendingInteractionQueue());
    }

    private static string? GetHeldItemID(DataStore store, int playerEntity)
    {
        if (!store.TryGet(playerEntity, out EquipmentComponent equipment) ||
            !store.TryGet(playerEntity, out InventoryComponent inventory))
        {
            return null;
        }

        int slot = equipment.ActiveInventorySlot;
        if (slot < 0 || slot >= inventory.Contents.Length)
        {
            return null;
        }

        return inventory.Contents[slot].ID;
    }
    
    private void OnMiddleClick()
    {
        if (!TryGetBrickFromScreenSpace(false, false, out Entity clickedEntity, out Voxel clickedVoxel, out Int3 brickPos, out VoxelComponent voxelComponent, out TransformComponent transformComponent))
        {
            return;
        }

        //  Clone the target brick's shape
        ShapeLight shapeLight = clickedVoxel.ShapeLight;
        _interactionState.SelectedShape.Set(shapeLight.Shape);
        
        //  If the player has a valid item, select it
        (_store ?? throw new InvalidOperationException("Interaction attempted before the ECS store was available.")).Query<PlayerComponent, InventoryComponent>(0f, PlayerInventoryQuery);
        void PlayerInventoryQuery(float delta, DataStore store, int playerEntity, in PlayerComponent player, in InventoryComponent inventory)
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
                store.QueryRef<EquipmentComponent>(playerEntity, 0f, UpdateActiveSlotQuery);
                void UpdateActiveSlotQuery(float _, DataStore dataStore, int entity, ref Ref<EquipmentComponent> equipment)
                {
                    if (i >= Hotbar.SLOT_COUNT)
                    {
                        playerInventory.Swap(equipment.Read.ActiveInventorySlot, i);
                        dataStore.MarkDirty<InventoryComponent>(entity);
                    }
                    else
                    {
                        equipment.Write.ActiveInventorySlot = i;
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
            _activeGizmo.Visible = false;
            _debugLines[0].Color = Vector4.Zero;
            _debugLines[1].Color = Vector4.Zero;
            _debugLines[2].Color = Vector4.Zero;
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
        
        Quaternion placeableOrientation = holdingPlaceable ? GetPlacementWorldQuaternion(transformComponent, clickedPoint, worldPos) : transformComponent.Orientation * new Orientation(clickedVoxel.Orientation).ToQuaternion();

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
            placeableShape = placeableBrickInfo.Shapeable ? _interactionState.SelectedShape.Get() : placeableBrickInfo.Shape;
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
    
    private Quaternion GetPlacementWorldQuaternion(TransformComponent transformComponent, Vector3 clickedPos, Vector3 brickPosWorld)
    {
        return transformComponent.Orientation * GetPlacementLocalQuaternion(transformComponent, clickedPos, brickPosWorld);
    }
    
    
    private Quaternion GetPlacementLocalQuaternion(TransformComponent transformComponent, Vector3 clickedPos, Vector3 brickPosWorld)
    {
        CameraEntity camera = _renderContext.MainCamera.Get();
        Quaternion baseQuaternion = GetPlacementLocalQuaternion(
            clickedPos, 
            brickPosWorld, 
            camera.Transform.Position, 
            camera.Transform.Orientation, 
            transformComponent.Orientation
        );

        Orientation selectedOrientation = _interactionState.SelectedOrientation.Get();
        var offsetQuaternion = selectedOrientation.ToQuaternion(); 
        Quaternion quaternion = baseQuaternion * offsetQuaternion;
        
        return quaternion;
    }
    
    private Orientation GetPlacementLocalOrientation(TransformComponent transformComponent, Vector3 clickedPos, Vector3 brickPosWorld)
    {
        Quaternion quaternion = GetPlacementLocalQuaternion(transformComponent, clickedPos, brickPosWorld);
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

        var placementNormal = _interactionState.SnapPlacement ? clickedSurfaceNormal : brickToCameraNormal;

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

    bool IDebugOverlay.IsVisible() => WaywardBeyond.IsPlaying();
    Result IDebugOverlay.RenderDebugOverlay(double delta, UIBuilder<Material> ui)
    {
        DebugInfo debugInfo = _debugInfo;

        Result<BrickInfo> brickInfoResult = _brickDatabase.Get(debugInfo.Voxel.ID);
        string brickID = brickInfoResult.Success ? brickInfoResult.Value.ID : "UNKNOWN";
        var shapeLight = new ShapeLight(debugInfo.Voxel.ShapeLight);
        
        using (ui.Text($"Voxel: {debugInfo.Voxel.ID} ({brickID})")) {}
        using (ui.Text($"Light: {shapeLight.LightLevel}")) {}
        using (ui.Text($"Shape: {shapeLight.Shape}")) {}
        using (ui.Text($"Coordinate: {debugInfo.Coordinate}")) {}
        using (ui.Text($"Position: {debugInfo.Position:N3}")) {}
        using (ui.Text($"Normal: {debugInfo.Normal:N0}")) {}
        using (ui.Text($"Align (Cam): {debugInfo.AlignmentCamera:N3}")) {}
        using (ui.Text($"Align (Sur): {debugInfo.AlignmentSurface:N3}")) {}
        using (ui.Text($"Align (Vert): {debugInfo.AlignmentFloor}")) {}
        using (ui.Text($"Snap: {_interactionState.SnapPlacement.Get()}")) {}
        
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
        
        //  If offset is true, then this raycast is attempting to be adjacent to a voxel.
        //  If the hit voxel isn't empty, then march back along the ray to try and find an empty voxel.
        for (var step = 0; offset && voxel.ID != 0 && step < 10; step++)
        {
            worldPos -= raycast.Normal * 0.25f;
            coordinate = WorldToBrickSpace(worldPos, transformComponent.Position, transformComponent.Orientation);
            voxel = voxelComponent.VoxelObject.Get(coordinate.X, coordinate.Y, coordinate.Z);
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
        Result<ItemSlot> mainHandResult = _playerData.GetMainHand(_store ?? throw new InvalidOperationException("Interaction attempted before the ECS store was available."));
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
}