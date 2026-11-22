using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using Krill;
using Reef;
using Shoal.Modularity;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Library.Collections;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Types;
using Swordfish.Library.Util;
using Swordfish.Physics;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Debug;
using WaywardBeyond.Client.Events;
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Client.UI.Layers;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Gameplay.Networking;
using WaywardBeyond.Items;
using WaywardBeyond.Voxels;

using WaywardBeyond.Client.Voxels.Processing;
using WaywardBeyond.Data.Networking;

namespace WaywardBeyond.Client.Systems;

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

internal sealed class PlayerInteractionService : IEntryPoint, IEntitySystem, IDebugOverlay
{
    private const float OUTLINE_OUTSET = 1.035f;
    private static readonly Vector4 _outlineColor = new(0.5f, 0.5f, 0.5f, 0.8f);
    private static readonly Vector4 _invalidOutlineColor = new(0.95f, 0.25f, 0.25f, 0.9f);
    private static readonly Vector3[] _worldAxes = [Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ];

    private readonly InteractionState _interactionState;
    private readonly BrickPreviewState _brickPreviewState;
    private readonly IInputService _inputService;
    private readonly IPhysics _physics;
    private readonly ILineRenderer _lineRenderer;
    private readonly IRenderContext _renderContext;
    private readonly IWindowContext _windowContext;
    private readonly PlayerData _playerData;
    private readonly IBrickDatabase _brickDatabase;
    private readonly IBrickRegistry _brickRegistry;
    private readonly IPaintDatabase? _paintDatabase;
    private readonly IPaintRegistry? _paintRegistry;
    private readonly ItemDatabase _itemDatabase;
    private readonly IAssetDatabase<Mesh> _meshDatabase;
    private readonly Dictionary<BrickShape, MeshOutline> _shapeOutlines;
    private readonly Dictionary<Mesh, MeshOutline> _meshOutlines = [];
    private MeshOutline? _activeOutline;
    private readonly Line[] _debugLines;
    private readonly DebugSettings _debugSettings;
    private readonly SoundEffectService _soundEffectService;
    private readonly VoxelLightingService _voxelLightingService;
    private readonly EventInvoker<PlaceEvent> _placeEvent;
    private readonly EventInvoker<BreakEvent> _breakEvent;
    private readonly SnapshotAckTracker _snapshotAck;
    private readonly NotificationService _notificationService;
    private readonly ILocalization _localization;

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
    private volatile string? _heldItemID;

    public PlayerInteractionService(
        in InteractionState interactionState,
        in BrickPreviewState brickPreviewState,
        in IInputService inputService,
        in IPhysics physics,
        in ILineRenderer lineRenderer,
        in IRenderContext renderContext,
        in IWindowContext windowContext,
        in PlayerData playerData,
        in IBrickDatabase brickDatabase,
        in IBrickRegistry brickRegistry,
        in ItemDatabase itemDatabase,
        in DebugSettings debugSettings,
        in IAssetDatabase<Mesh> meshDatabase,
        in IShortcutService shortcutService,
        in SoundEffectService soundEffectService,
        in VoxelLightingService voxelLightingService,
        in EventInvoker<PlaceEvent> placeEvent,
        in EventInvoker<BreakEvent> breakEvent,
        in SnapshotAckTracker snapshotAck,
        in NotificationService notificationService,
        in ILocalization localization,
        IPaintDatabase? paintDatabase = null,
        IPaintRegistry? paintRegistry = null
    ) {
        _interactionState = interactionState;
        _brickPreviewState = brickPreviewState;
        _inputService = inputService;
        _physics = physics;
        _lineRenderer = lineRenderer;
        _renderContext = renderContext;
        _windowContext = windowContext;
        _playerData = playerData;
        _brickDatabase = brickDatabase;
        _brickRegistry = brickRegistry;
        _paintDatabase = paintDatabase;
        _paintRegistry = paintRegistry;
        _itemDatabase = itemDatabase;
        _meshDatabase = meshDatabase;
        _debugSettings = debugSettings;
        _soundEffectService = soundEffectService;
        _voxelLightingService = voxelLightingService;
        _placeEvent = placeEvent;
        _breakEvent = breakEvent;
        _snapshotAck = snapshotAck;
        _notificationService = notificationService;
        _localization = localization;

        Mesh slope = meshDatabase.Get("slope.obj").Value;
        Mesh stair = meshDatabase.Get("stair.obj").Value;
        Mesh slab = meshDatabase.Get("slab.obj").Value;
        Mesh column = meshDatabase.Get("column.obj").Value;
        Mesh plate = meshDatabase.Get("plate.obj").Value;

        _shapeOutlines = new Dictionary<BrickShape, MeshOutline>
        {
            { BrickShape.Any, new MeshOutline(lineRenderer, _outlineColor, new Cube(), outset: OUTLINE_OUTSET)},
            { BrickShape.Custom, new MeshOutline(lineRenderer, _outlineColor, new Cube(), outset: OUTLINE_OUTSET)},
            { BrickShape.Block, new MeshOutline(lineRenderer, _outlineColor, new Cube(), outset: OUTLINE_OUTSET)},
            { BrickShape.Slab, new MeshOutline(lineRenderer, _outlineColor, slab, outset: OUTLINE_OUTSET)},
            { BrickShape.Stair, new MeshOutline(lineRenderer, _outlineColor, stair, outset: OUTLINE_OUTSET)},
            { BrickShape.Slope, new MeshOutline(lineRenderer, _outlineColor, slope, outset: OUTLINE_OUTSET)},
            { BrickShape.Column, new MeshOutline(lineRenderer, _outlineColor, column, outset: OUTLINE_OUTSET)},
            { BrickShape.Plate, new MeshOutline(lineRenderer, _outlineColor, plate, outset: OUTLINE_OUTSET)},
        };

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

        if (_debugSettings.OverlayVisible)
        {
            HeldItemQuery query = default;
            store.Query<PlayerComponent, EquipmentComponent, InventoryComponent, HeldItemQuery>(delta, ref query);
            _heldItemID = query.HeldItemID;
        }
        else
        {
            _heldItemID = null;
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
        if (heldItemID != null && TryGetPaintToolMode(heldItemID, out PaintKind paintMode))
        {
            AttemptPaintInteraction(store, playerEntity, kind, mode, paintMode);
            return;
        }

        Brick? placeable = null;
        if (isPlace && heldItemID != null)
        {
            Result<Brick> placeableResult = ItemPlaceables.ResolvePlaceable(_itemDatabase, _brickDatabase, heldItemID);
            if (placeableResult.Success)
            {
                placeable = placeableResult.Value;
            }
        }

        var world = new ClientVoxelInteractionWorld(store, _physics);

        //  Resolve the target structure + cell and the placement orientation from the same screen-space
        //  raycast the ghost preview uses, so the placed brick always matches the previewed orientation.
        if (!TryGetBrickFromScreenSpace(isPlace, reachAround: true, out Entity entity, out Voxel targetVoxel, out Int3 coordinate, out _, out TransformComponent targetTransform, out Vector3 clickedPoint))
        {
            return;
        }

        if (isPlace && placeable != null && targetVoxel.ID != 0)
        {
            _notificationService.Push(new Notification(_localization.GetRequired("notification.placement.occupied"), NotificationType.Action));
            return;
        }

        BrickInteraction hint = BuildInteractionHint(isPlace, placeable, store, coordinate, entity.Ptr, in targetTransform, clickedPoint);

        if (isPlace && placeable != null)
        {
            BrickShape resolvedShape = placeable.Shapeable ? (BrickShape)hint.HintShape : placeable.Shape;
            Orientation resolvedOrientation = placeable.IsOrientable(resolvedShape)
                ? new Orientation(hint.HintOrientation)
                : Orientation.Identity;
            if (InteractionResolver.IntersectsCollider(_physics, entity.Ptr, in targetTransform, in coordinate, placeable, resolvedShape, resolvedOrientation))
            {
                _notificationService.Push(new Notification(_localization.GetRequired("notification.placement.occupied"), NotificationType.Action));
                return;
            }
        }

        Vector3 origin = store.TryGet(playerEntity, out TransformComponent playerTransform) ? playerTransform.Position : Vector3.Zero;
        var request = new InteractionRequest(origin, hint, kind, placeable, mode, CharacterDefaults.DEFAULT_REACH);
        InteractionResolution resolution = InteractionResolver.Resolve(in request, world, _brickRegistry);
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

    private static bool TryGetPaintToolMode(Item item, out PaintKind mode)
    {
        if (item.Tool?.Type == ToolType.Sprayer)
        {
            mode = PaintKind.Color;
            return true;
        }

        if (item.Tool?.Type == ToolType.Drill)
        {
            mode = PaintKind.Texture;
            return true;
        }

        mode = default;
        return false;
    }

    private bool TryGetPaintToolMode(string itemId, out PaintKind mode)
    {
        Result<Item> itemResult = _itemDatabase.Get(itemId);
        if (itemResult.Success)
        {
            return TryGetPaintToolMode(itemResult.Value, out mode);
        }

        mode = default;
        return false;
    }

    private void AttemptPaintInteraction(DataStore store, int playerEntity, InteractionKind kind, GameMode gameMode, PaintKind paintMode)
    {
        if (_paintDatabase == null || _paintRegistry == null)
        {
            return;
        }

        bool hit = TryGetBrickFromScreenSpace(
            offset: false,
            reachAround: false,
            out Entity entity,
            out Voxel targetVoxel,
            out Int3 coordinate,
            out VoxelComponent voxelComponent,
            out TransformComponent targetTransform,
            out _,
            out Vector3 hitNormal
        );
        if (!hit || targetVoxel.ID == 0)
        {
            return;
        }

        BrickFace hitFace = PaintInteractionResolver.WorldNormalToBrickFace(hitNormal, targetTransform.Orientation, targetVoxel.GetOrientation());
        string? paintId = null;
        if (kind == InteractionKind.PrimaryPressed)
        {
            paintId = GetActiveOrDefaultPaintId(paintMode);
            PaintTargetValidation validation = PaintInteractionResolver.ValidateApplyTarget(
                in targetVoxel,
                paintMode,
                paintId,
                _brickDatabase,
                _paintDatabase,
                _paintRegistry
            );
            if (validation != PaintTargetValidation.Valid)
            {
                string key = validation == PaintTargetValidation.AlphaMismatch
                    ? "notification.paint.incompatibleTexture"
                    : "notification.paint.invalidTarget";
                _notificationService.Push(new Notification(_localization.GetRequired(key), NotificationType.Action));
                return;
            }
        }
        else if (kind == InteractionKind.SecondaryPressed)
        {
            BrickShape shape = targetVoxel.GetShapeLight().Shape;
            BrickFaceMask expandedMask = PaintInteractionResolver.ExpandFaceMask(hitFace.ToMask(), shape);
            if (!PaintInteractionResolver.HasRemovableOverride(voxelComponent.VoxelObject, in coordinate, expandedMask, paintMode))
            {
                _notificationService.Push(new Notification(_localization.GetRequired("notification.paint.nothingToRemove"), NotificationType.Action));
                return;
            }
        }

        var paintHint = new PaintInteraction(
            store.GetUuid(entity.Ptr).ToValue(),
            coordinate.X,
            coordinate.Y,
            coordinate.Z,
            (byte)hitFace.ToMask(),
            (byte)paintMode,
            paintId
        );

        Vector3 origin = store.TryGet(playerEntity, out TransformComponent playerTransform) ? playerTransform.Position : Vector3.Zero;
        var world = new ClientVoxelInteractionWorld(store, _physics);
        var request = new InteractionRequest(origin, hint: null, kind, placeable: null, gameMode, CharacterDefaults.DEFAULT_REACH, paintHint);
        InteractionResolution resolution = PaintInteractionResolver.Resolve(in request, world, _brickDatabase, _paintDatabase, _paintRegistry);
        if (resolution.Action != InteractionAction.Paint)
        {
            if (kind == InteractionKind.PrimaryPressed || kind == InteractionKind.SecondaryPressed)
            {
                _notificationService.Push(new Notification(_localization.GetRequired("notification.paint.invalidTarget"), NotificationType.Action));
            }

            return;
        }

        ApplyFacePaintPrediction(store, playerEntity, kind, in resolution, in paintHint, voxelComponent);
    }

    private string? GetActiveOrDefaultPaintId(PaintKind paintMode)
    {
        string? paintId = paintMode == PaintKind.Color
            ? _interactionState.SelectedColorPaintId.Get()
            : _interactionState.SelectedTexturePaintId.Get();
        if (!string.IsNullOrEmpty(paintId) || _paintDatabase == null)
        {
            return paintId;
        }

        IReadOnlyList<Paint> fallbackList = paintMode == PaintKind.Color
            ? _paintDatabase.ColorPaints
            : _paintDatabase.TexturePaints;
        return fallbackList.Count > 0 ? fallbackList[0].ID : null;
    }

    private bool IsPaintTargetValid(in Voxel targetVoxel, PaintKind paintMode)
    {
        if (_paintDatabase == null || _paintRegistry == null)
        {
            return false;
        }

        string? paintId = GetActiveOrDefaultPaintId(paintMode);
        return PaintInteractionResolver.ValidateApplyTarget(
            in targetVoxel,
            paintMode,
            paintId,
            _brickDatabase,
            _paintDatabase,
            _paintRegistry
        ) == PaintTargetValidation.Valid;
    }

    private void ApplyFacePaintPrediction(
        DataStore store,
        int playerEntity,
        InteractionKind kind,
        in InteractionResolution resolution,
        in PaintInteraction hint,
        VoxelComponent voxelComponent
    ) {
        VoxelObject voxelObject = voxelComponent.VoxelObject;
        Int3 coordinate = resolution.Coordinate;
        PaintResolution paint = resolution.Paint;

        var originalOverrides = new FaceOverride[6];
        for (var f = 0; f < 6; f++)
        {
            voxelObject.TryGetFaceOverride(coordinate.X, coordinate.Y, coordinate.Z, (BrickFace)f, out originalOverrides[f]);
        }

        if (paint.Mode == PaintKind.Texture)
        {
            voxelObject.SetFaceTexture(coordinate.X, coordinate.Y, coordinate.Z, paint.FaceMask, paint.PaintId);
        }
        else
        {
            voxelObject.SetFaceColor(coordinate.X, coordinate.Y, coordinate.Z, paint.FaceMask, paint.PaintId);
        }

        voxelComponent.Registry.MarkFaceModified(coordinate.X, coordinate.Y, coordinate.Z, voxelObject.ChunkSize);
        store.MarkDirty<VoxelComponent>(resolution.Entity);

        PendingInteractionComponent pending = GetOrCreatePending(store, playerEntity);
        uint sequence = ++_interactionSequence;
        uint serverTickAtSample = _snapshotAck.LastAppliedSnapshotTick;
        pending.Queue.RegisterFaceEdit(new PendingFaceEdit(
            resolution.Entity,
            coordinate,
            paint.FaceMask,
            paint.Mode,
            originalOverrides,
            paint.CanonicalPaintId,
            sequence,
            serverTickAtSample
        ));

        var interaction = new InteractionEvent
        {
            Entity = store.GetUuid(playerEntity).ToValue(),
            SequenceNumber = sequence,
            ServerTickAtSample = serverTickAtSample,
            Kind = kind,
            Paint = hint,
        };

        pending.Outbound.Stage(interaction);
        store.AddOrUpdate(playerEntity, pending);
    }

    private BrickInteraction BuildInteractionHint(bool isPlace, Brick? placeable, DataStore store, Int3 coordinate, int targetEntity, in TransformComponent targetTransform, Vector3 clickedPoint)
    {
        byte hintShape = 0;
        byte hintOrientation = 0;

        if (isPlace && placeable != null)
        {
            BrickShape shape = placeable.Shapeable ? _interactionState.SelectedShape.Get() : placeable.Shape;
            hintShape = (byte)shape;
            hintOrientation = ResolvePlacementOrientation(in targetTransform, clickedPoint, coordinate);
        }

        return new BrickInteraction
        {
            TargetEntity = store.GetUuid(targetEntity).ToValue(),
            TargetX = coordinate.X,
            TargetY = coordinate.Y,
            TargetZ = coordinate.Z,
            HintShape = hintShape,
            HintOrientation = hintOrientation,
        };
    }

    /// <summary>
    /// Resolves the placement orientation from the same hit the ghost preview uses: the target structure's
    /// click point + cell, so the placed brick matches the previewed orientation. No separate raycast.
    /// </summary>
    private byte ResolvePlacementOrientation(in TransformComponent transform, Vector3 clickedPos, Int3 brickCoordinate)
    {
        Vector3 worldPos = BrickToWorldSpace(brickCoordinate, transform.Position, transform.Orientation);
        Orientation orientation = GetPlacementLocalOrientation(transform, clickedPos, worldPos);
        return orientation.ToByte();
    }

    private bool FirePresentationHook(in InteractionResolution resolution, InteractionKind kind)
    {
        Result<Brick> brickInfoResult = _brickDatabase.Get(resolution.Voxel.ID);
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
        Voxel predictedForRecord = predicted;

        //  Predict the authoritative outcome onto the presentation-only voxel container. The reconcile
        //  system confirms, snaps, or reverts this against the server's authoritative VoxelEditMessage.
        voxelObject.Set(coordinate.X, coordinate.Y, coordinate.Z, predicted);
        _voxelLightingService.UpdateLighting(voxelObject, voxelComponent.Registry, coordinate.X, coordinate.Y, coordinate.Z, in original, ref predicted);

        //  Publish the edit by marking the voxel component dirty; the VoxelEntityRebuildSystem observes
        //  this flag and fulfills the mesh/collider rebuild on the ECS thread.
        store.MarkDirty<VoxelComponent>(resolution.Entity);

        PendingInteractionComponent pending = GetOrCreatePending(store, playerEntity);
        uint sequence = ++_interactionSequence;
        uint serverTickAtSample = _snapshotAck.LastAppliedSnapshotTick;
        pending.Queue.Register(resolution.Entity, coordinate, original, predictedForRecord, sequence, serverTickAtSample);

        var interaction = new InteractionEvent
        {
            Entity = store.GetUuid(playerEntity).ToValue(),
            SequenceNumber = sequence,
            ServerTickAtSample = serverTickAtSample,
            Kind = kind,
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

        ItemData itemData = inventory.Contents[slot];
        return itemData.Count > 0 ? itemData.ID : null;
    }
    
    private void OnMiddleClick()
    {
        if (WaywardBeyond.GameState != GameState.Playing || _interactionState.IsInteractionBlocked())
        {
            return;
        }

        bool hit = TryGetBrickFromScreenSpace(
            offset: false,
            reachAround: false,
            out Entity clickedEntity,
            out Voxel clickedVoxel,
            out Int3 brickPos,
            out VoxelComponent voxelComponent,
            out TransformComponent transformComponent,
            out _,
            out Vector3 hitNormal
        );
        if (!hit || clickedVoxel.ID == 0)
        {
            return;
        }

        DataStore store = _store ?? throw new InvalidOperationException("Interaction attempted before the ECS store was available.");
        Result<ItemSlot> mainHandResult = _playerData.GetMainHand(store);
        if (mainHandResult.Success && TryGetPaintToolMode(mainHandResult.Value.Item, out PaintKind paintMode))
        {
            PickPaintForTool(voxelComponent.VoxelObject, in brickPos, in clickedVoxel, in transformComponent, hitNormal, paintMode);
            return;
        }

        //  Clone the target brick's shape
        ShapeLight shapeLight = clickedVoxel.ShapeLight;
        _interactionState.SelectedShape.Set(shapeLight.Shape);

        //  If the player has a matching pickable item, equip it: select it when it is already in the
        //  hotbar, otherwise move the whole stack into the active hotbar slot. Both paths replicate
        //  upstream - an active-slot change rides the dirty ClientOwned EquipmentComponent, a hotbar
        //  move rides an InventoryEvent via PlayerData.ApplyMove - so the server stays authoritative.
        int pickSlot = FindPickSlot(store, clickedVoxel.ID);
        if (pickSlot < 0)
        {
            return;
        }

        int activeSlot = _playerData.GetActiveSlot(store).Value;
        if (pickSlot >= Hotbar.SLOT_COUNT)
        {
            _playerData.ApplyMove(store, new SlotMoveOp
            {
                Mode = SlotMoveOpMode.Exact,
                FromSlot = pickSlot,
                ToSlot = activeSlot,
                Count = null,
            });
        }
        else
        {
            _playerData.SetActiveSlot(store, pickSlot);
        }
    }

    private void PickPaintForTool(
        VoxelObject voxelObject,
        in Int3 coordinate,
        in Voxel clickedVoxel,
        in TransformComponent transformComponent,
        Vector3 hitNormal,
        PaintKind paintMode
    ) {
        if (_paintDatabase == null)
        {
            return;
        }

        BrickFace hitFace = PaintInteractionResolver.WorldNormalToBrickFace(hitNormal, transformComponent.Orientation, clickedVoxel.GetOrientation());
        Result<Paint> pickedResult = PaintInteractionResolver.PickFacePaint(
            voxelObject,
            in coordinate,
            in clickedVoxel,
            hitFace,
            paintMode,
            _brickDatabase,
            _paintDatabase
        );
        if (!pickedResult.Success)
        {
            return;
        }

        Paint picked = pickedResult.Value;
        if (picked.Kind == PaintKind.Color)
        {
            _interactionState.SelectColorPaint(picked.ID);
        }
        else
        {
            _interactionState.SelectTexturePaint(picked.ID);
        }
    }

    private int FindPickSlot(DataStore store, int brickID)
    {
        Result<InventoryComponent> inventoryResult = _playerData.GetInventory(store);
        if (!inventoryResult.Success || inventoryResult.Value.Contents == null)
        {
            return -1;
        }

        ItemData[] contents = inventoryResult.Value.Contents;
        for (var i = 0; i < contents.Length; i++)
        {
            string itemID = contents[i].ID;
            Result<Item> itemResult = _itemDatabase.Get(itemID);
            if (!itemResult.Success || itemResult.Value.Placeable == null)
            {
                continue;
            }

            Result<Brick> placeableBrickResult = _brickDatabase.Get(itemResult.Value.Placeable.Value.ID);
            if (placeableBrickResult.Success && placeableBrickResult.Value.DataID == brickID)
            {
                return i;
            }
        }

        return -1;
    }

    private void OnFixedUpdate(object? sender, EventArgs e)
    {
        if (WaywardBeyond.GameState < GameState.Playing || _store == null)
        {
            HideInteractionVisuals();
            return;
        }

        Result<ItemSlot> mainHandResult = _playerData.GetMainHand(_store);
        Result<Brick> placeableResult = TryGetPlaceableBrick(mainHandResult);
        bool holdingPlaceable = placeableResult.Success;
        PaintKind paintToolMode = default;
        bool holdingPaintTool = !holdingPlaceable
            && mainHandResult.Success
            && TryGetPaintToolMode(mainHandResult.Value.Item, out paintToolMode);

        if (!TryGetBrickFromScreenSpace(holdingPlaceable, reachAround: !holdingPaintTool, out Entity entity, out Voxel clickedVoxel, out Int3 brickPos, out VoxelComponent voxelComponent, out TransformComponent transformComponent, out Vector3 clickedPoint)
            || !holdingPlaceable && clickedVoxel.ID == 0)
        {
            _debugInfo = default;
            HideInteractionVisuals();
            return;
        }

        Vector3 worldPos = BrickToWorldSpace(brickPos, transformComponent.Position, transformComponent.Orientation);
        
        _debugInfo.VoxelComponent = voxelComponent;
        _debugInfo.Voxel = clickedVoxel;
        _debugInfo.Coordinate = brickPos;
        _debugInfo.Position = worldPos;

        Quaternion localPlacementOrientation = holdingPlaceable
            ? GetPlacementLocalQuaternion(transformComponent, clickedPoint, worldPos)
            : Quaternion.Identity;

        Quaternion placeableOrientation = holdingPlaceable
            ? transformComponent.Orientation * localPlacementOrientation
            : transformComponent.Orientation * clickedVoxel.GetOrientation().ToQuaternion();

        UpdateDebugLines(worldPos, placeableOrientation);

        if (holdingPlaceable)
        {
            Brick placeableBrick = placeableResult.Value;
            BrickShape shape = placeableBrick.Shapeable ? _interactionState.SelectedShape.Get() : placeableBrick.Shape;
            Orientation previewOrientation = placeableBrick.IsOrientable(shape)
                ? new Orientation(localPlacementOrientation)
                : Orientation.Identity;
            bool isPlacementValid = clickedVoxel.ID == 0
                && !InteractionResolver.IntersectsCollider(_physics, entity.Ptr, in transformComponent, in brickPos, placeableBrick, shape, previewOrientation);
            if (isPlacementValid)
            {
                _brickPreviewState.Show(placeableBrick, shape, previewOrientation, worldPos, transformComponent.Orientation);
            }
            else
            {
                _brickPreviewState.Hide();
            }

            Vector4 outlineColor = isPlacementValid ? _outlineColor : _invalidOutlineColor;
            ShowPlacementOutline(placeableBrick, shape, worldPos, transformComponent.Orientation, previewOrientation, outlineColor);
            return;
        }

        _brickPreviewState.Hide();
        Vector4 selectionColor = holdingPaintTool && !IsPaintTargetValid(in clickedVoxel, paintToolMode)
            ? _invalidOutlineColor
            : _outlineColor;
        ShowSelectionOutline(new ShapeLight(clickedVoxel.ShapeLight).Shape, clickedVoxel, worldPos, placeableOrientation, selectionColor);
    }

    private void HideInteractionVisuals()
    {
        HideOutline();
        _debugLines[0].Color = Vector4.Zero;
        _debugLines[1].Color = Vector4.Zero;
        _debugLines[2].Color = Vector4.Zero;
        _brickPreviewState.Hide();
    }

    private void UpdateDebugLines(Vector3 worldPos, Quaternion orientation)
    {
        if (!_debugSettings.OverlayVisible)
        {
            _debugLines[0].Color = Vector4.Zero;
            _debugLines[1].Color = Vector4.Zero;
            _debugLines[2].Color = Vector4.Zero;
            return;
        }

        _debugLines[0].Color = new Vector4(1, 0, 0, 1);
        _debugLines[0].Start = worldPos;
        _debugLines[0].End = worldPos + Vector3.Transform(Vector3.UnitX, orientation);
        
        _debugLines[1].Color = new Vector4(0, 1, 0, 1);
        _debugLines[1].Start = worldPos;
        _debugLines[1].End = worldPos + Vector3.Transform(Vector3.UnitY, orientation);
        
        _debugLines[2].Color = new Vector4(0, 0, 1, 1);
        _debugLines[2].Start = worldPos;
        _debugLines[2].End = worldPos + Vector3.Transform(Vector3.UnitZ, orientation);
    }

    private void ShowSelectionOutline(BrickShape shape, in Voxel voxel, Vector3 worldPos, Quaternion orientation, Vector4 color)
    {
        Mesh? customMesh = null;

        if (shape == BrickShape.Custom)
        {
            Result<Brick> clickedBrickResult = _brickDatabase.Get(voxel.ID);
            if (clickedBrickResult.Success)
            {
                customMesh = ResolveMesh(clickedBrickResult.Value);
            }
        }

        ShowOutline(shape, customMesh, worldPos, orientation, color);
    }

    private void ShowPlacementOutline(
        Brick brick,
        BrickShape shape,
        Vector3 worldPos,
        Quaternion structureOrientation,
        Orientation orientation,
        Vector4 color
    ) {
        Mesh? customMesh = shape == BrickShape.Custom ? ResolveMesh(brick) : null;
        ShowOutline(shape, customMesh, worldPos, structureOrientation * orientation.ToQuaternion(), color);
    }

    private void ShowOutline(BrickShape shape, Mesh? customMesh, Vector3 worldPos, Quaternion orientation, Vector4 color)
    {
        MeshOutline? outline = null;

        if (shape == BrickShape.Custom)
        {
            outline = GetMeshOutline(customMesh);
        }

        if (outline == null && _shapeOutlines.TryGetValue(shape, out MeshOutline? shapeOutline))
        {
            outline = shapeOutline;
        }

        if (outline == null)
        {
            HideOutline();
            return;
        }

        if (_activeOutline != outline)
        {
            HideOutline();
            _activeOutline = outline;
        }

        _activeOutline.Color = color;
        _activeOutline.Visible = true;
        _activeOutline.Render(new TransformComponent(worldPos, orientation, Vector3.One));
    }

    private MeshOutline? GetMeshOutline(Mesh? mesh)
    {
        if (mesh == null)
        {
            return null;
        }

        if (!_meshOutlines.TryGetValue(mesh, out MeshOutline? outline))
        {
            outline = new MeshOutline(_lineRenderer, _outlineColor, mesh, outset: OUTLINE_OUTSET);
            _meshOutlines.Add(mesh, outline);
        }

        return outline;
    }

    private void HideOutline()
    {
        if (_activeOutline == null)
        {
            return;
        }

        _activeOutline.Visible = false;
        _activeOutline = null;
    }
    
    private Mesh? ResolveMesh(in Brick brickInfo)
    {
        if (brickInfo.MeshID == null)
        {
            return null;
        }

        Result<Mesh> meshResult = _meshDatabase.Get(brickInfo.MeshID);
        return meshResult.Success ? meshResult.Value : null;
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

        Result<Brick> brickInfoResult = _brickDatabase.Get(debugInfo.Voxel.ID);
        string brickID = brickInfoResult.Success ? brickInfoResult.Value.ID : (_brickRegistry.Name(debugInfo.Voxel.ID) ?? "UNKNOWN");
        var shapeLight = new ShapeLight(debugInfo.Voxel.ShapeLight);
        string heldItemID = string.IsNullOrEmpty(_heldItemID) ? "None" : _heldItemID;

        using (ui.Text($"Held item: {heldItemID}")) {}
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
        return TryGetBrickFromScreenSpace(
            offset,
            reachAround,
            out entity,
            out voxel,
            out coordinate,
            out voxelComponent,
            out transformComponent,
            out clickedPoint,
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
        out Vector3 clickedPoint,
        out Vector3 hitNormal
    ) {
        CameraEntity cameraEntity = _renderContext.MainCamera.Get();

        Vector3? reachAroundDir = null;
        Ray ray = cameraEntity.ScreenPointToRay((int)_windowContext.Resolution.X / 2, (int)_windowContext.Resolution.Y / 2, (int)_windowContext.Resolution.X, (int)_windowContext.Resolution.Y);
        //  TODO #319 offset origin by the player's collider without hardcoded value or allow raycasting against a layer mask
        const float ORIGIN_OFFSET = 0.26f;
        ray = new Ray(ray.Origin + ORIGIN_OFFSET * ray.Vector, ray.Vector * (CharacterDefaults.DEFAULT_REACH - ORIGIN_OFFSET));
        if (!TryRaycastBrickEntity(ray, out RaycastResult raycast, out voxelComponent, out transformComponent))
        {
            if (!reachAround || !TryReachAroundRaycasts(ray, cameraEntity, ref reachAroundDir, out voxelComponent, out transformComponent, out raycast))
            {
                entity = default;
                voxel = default;
                coordinate = default;
                clickedPoint = default;
                hitNormal = default;
                return false;
            }
        }
        
        clickedPoint = raycast.Point;
        hitNormal = raycast.Normal;
        coordinate = offset && reachAroundDir == null
            ? InteractionResolver.ResolvePlacementCell(
                raycast.Point,
                raycast.Normal,
                transformComponent.Position,
                transformComponent.Orientation
            )
            : InteractionResolver.ResolveHitCell(
                raycast.Point,
                raycast.Normal,
                transformComponent.Position,
                transformComponent.Orientation
            );
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
        return InteractionResolver.WorldToBrickSpace(position, origin, orientation);
    }

    private static Vector3 BrickToWorldSpace(Int3 coordinate, Vector3 origin, Quaternion orientation)
    {
        return InteractionResolver.BrickToWorldSpace(coordinate, origin, orientation);
    }
    
    private Result<Brick> TryGetPlaceableBrick(Result<ItemSlot> mainHandResult)
    {
        if (!mainHandResult.Success || mainHandResult.Value.Item.Placeable == null)
        {
            return new Result<Brick>(success: false, null!, mainHandResult.Message, mainHandResult.Exception);
        }
        
        ItemSlot mainHand = mainHandResult.Value;
        Item item = mainHand.Item;
        PlaceableDefinition placeable = item.Placeable.Value;

        if (placeable.Type != PlaceableType.Brick)
        {
            return new Result<Brick>(success: false, null!);
        }

        Result<Brick> brickInfoResult = _brickDatabase.Get(placeable.ID);
        if (!brickInfoResult.Success)
        {
            return new Result<Brick>(success: false, null!, brickInfoResult.Message, brickInfoResult.Exception);
        }
        
        return Result<Brick>.FromSuccess(brickInfoResult.Value);
    }
    
    private struct HeldItemQuery : IForEach<PlayerComponent, EquipmentComponent, InventoryComponent>
    {
        public string? HeldItemID;

        public void Execute(float delta, DataStore store, int entity, in PlayerComponent player, in EquipmentComponent equipment, in InventoryComponent inventory)
        {
            int slot = equipment.ActiveInventorySlot;
            if (inventory.Contents == null || slot < 0 || slot >= inventory.Contents.Length)
            {
                return;
            }

            ItemData itemData = inventory.Contents[slot];
            HeldItemID = itemData.Count > 0 ? itemData.ID : null;
        }
    }
}