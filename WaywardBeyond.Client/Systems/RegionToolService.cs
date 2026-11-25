using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using Krill;
using Microsoft.Extensions.Logging;
using NsdRpc;
using Shoal.Modularity;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Types;
using Swordfish.Library.Types.Shapes;
using Swordfish.Library.Util;
using Swordfish.Physics;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Globalization;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Blueprints;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Items;
using WaywardBeyond.Networking.Rpc;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Systems;

/// <summary>
/// Handles Blueprint Projector inputs (selection corners, copy, cut, paste, rotation, camera-relative
/// mirroring, air overwrite toggle, and free-space distance) on the ECS thread and sends validated
/// region RPCs to the server.
/// </summary>
internal sealed class RegionToolService : IEntryPoint, IEntitySystem
{
    private const float RAYCAST_ORIGIN_OFFSET = 0.26f;
    private const float FREE_SPACE_SCROLL_STEP = 1.5f;
    private const float OVERLAP_PENETRATION_EPSILON = 0.005f;

    private readonly IInputService _inputService;
    private readonly IPhysics _physics;
    private readonly IRenderContext _renderContext;
    private readonly IWindowContext _windowContext;
    private readonly PlayerData _playerData;
    private readonly InteractionState _interactionState;
    private readonly RegionToolState _toolState;
    private readonly RegionClient _regionClient;
    private readonly IBlueprintStorage _blueprintStorage;
    private readonly IBrickRegistry _brickRegistry;
    private readonly IBrickDatabase _brickDatabase;
    private readonly IPaintRegistry? _paintRegistry;
    private readonly NotificationService _notificationService;
    private readonly LocalizedFormatter _formatter;
    private readonly ILogger<RegionToolService> _logger;

    private readonly ConcurrentQueue<MouseButton> _pendingClicks = new();
    private readonly ConcurrentQueue<float> _pendingScrolls = new();
    private readonly ConcurrentQueue<RegionToolAction> _pendingActions = new();
    private readonly ConcurrentQueue<PendingSaveRequest> _pendingSaves = new();
    private readonly ConcurrentQueue<Action> _ecsCallbacks = new();

    private volatile bool _holdingProjector;
    private volatile bool _pendingEquipProjector;
    private bool _cutInFlight;
    private bool _pasteInFlight;
    private int _sessionGeneration;
    private bool _wasInGame;

    private int _overlapCacheRevision = -1;
    private Orientation _overlapCacheRotation;
    private MirrorAxes _overlapCacheMirror;
    private CompoundShape _overlapCacheShape;

    /// <summary>Initializes the region tool service.</summary>
    public RegionToolService(
        in IInputService inputService,
        in IShortcutService shortcutService,
        in IPhysics physics,
        in IRenderContext renderContext,
        in IWindowContext windowContext,
        in PlayerData playerData,
        in InteractionState interactionState,
        in RegionToolState toolState,
        in RegionClient regionClient,
        in IBlueprintStorage blueprintStorage,
        in IBrickRegistry brickRegistry,
        in IBrickDatabase brickDatabase,
        in NotificationService notificationService,
        in LocalizedFormatter formatter,
        in ILogger<RegionToolService> logger,
        IPaintRegistry? paintRegistry = null
    )
    {
        _inputService = inputService;
        _physics = physics;
        _renderContext = renderContext;
        _windowContext = windowContext;
        _playerData = playerData;
        _interactionState = interactionState;
        _toolState = toolState;
        _regionClient = regionClient;
        _blueprintStorage = blueprintStorage;
        _brickRegistry = brickRegistry;
        _brickDatabase = brickDatabase;
        _paintRegistry = paintRegistry;
        _notificationService = notificationService;
        _formatter = formatter;
        _logger = logger;

        RegisterShortcut(shortcutService, "Region Copy", Key.C, ShortcutModifiers.None, () => QueueAction(RegionToolAction.Copy));
        RegisterShortcut(shortcutService, "Region Cut", Key.X, ShortcutModifiers.None, () => QueueAction(RegionToolAction.Cut));
        RegisterShortcut(shortcutService, "Region Paste Mode", Key.V, ShortcutModifiers.None, () => QueueAction(RegionToolAction.TogglePasteMode));
        RegisterShortcut(shortcutService, "Region Rotate Yaw", Key.T, ShortcutModifiers.None, () => QueueAction(RegionToolAction.RotateYaw));
        RegisterShortcut(shortcutService, "Region Rotate Pitch", Key.T, ShortcutModifiers.Shift, () => QueueAction(RegionToolAction.RotatePitch));
        RegisterShortcut(shortcutService, "Region Mirror Horizontal", Key.F, ShortcutModifiers.None, () => QueueAction(RegionToolAction.MirrorHorizontal));
        RegisterShortcut(shortcutService, "Region Mirror Vertical", Key.F, ShortcutModifiers.Shift, () => QueueAction(RegionToolAction.MirrorVertical));
        RegisterShortcut(shortcutService, "Region Overwrite Air", Key.G, ShortcutModifiers.None, () => QueueAction(RegionToolAction.ToggleOverwriteAir));
    }

    /// <inheritdoc/>
    public void Run()
    {
        _inputService.Clicked += OnClicked;
        _inputService.Scrolled += OnScrolled;
    }

    /// <summary>Enqueues an action from UI controls such as the radial menu to run on the ECS thread.</summary>
    public void QueueAction(RegionToolAction action)
    {
        _pendingActions.Enqueue(action);
    }

    /// <summary>Enqueues a blueprint save on the ECS thread (from current selection if complete, else clipboard) and delivers the result.</summary>
    public void QueueSaveBlueprint(string name, Action<Result<BlueprintSummary>> onCompleted)
    {
        _pendingSaves.Enqueue(new PendingSaveRequest(name, onCompleted));
    }

    /// <summary>Loads <paramref name="blueprintId"/> onto the clipboard, switches to Paste mode, and selects the projector in the hotbar if present.</summary>
    public Result<BlueprintData> LoadBlueprintToClipboard(string blueprintId)
    {
        Result<BlueprintData> loaded = _blueprintStorage.Load(blueprintId);
        if (!loaded.Success)
        {
            _notificationService.Push(new Notification(_formatter.GetString("notification.blueprint.loadFailed")));
            return loaded;
        }

        BlueprintData data = loaded.Value;
        VoxelRegionData encoded = data.ToRegion();
        VoxelRegionData decoded = VoxelCodec.Decode(in encoded, _brickRegistry, _paintRegistry);
        int brickCount = VoxelRegionApplier.CountSolidVoxels(in decoded);
        if (brickCount <= 0)
        {
            NotifyAction(_formatter.GetString("notification.region.empty"));
            return Result<BlueprintData>.FromFailure("Blueprint contains no bricks.");
        }

        _toolState.SetClipboard(in decoded, brickCount, data.Name, enterPasteMode: true);
        _pendingEquipProjector = true;
        NotifyAction(_formatter.GetString("notification.blueprint.loaded", data.Name));
        return loaded;
    }

    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        if (!WaywardBeyond.IsInGame())
        {
            if (_wasInGame)
            {
                _sessionGeneration++;
                _wasInGame = false;
                _cutInFlight = false;
                _pasteInFlight = false;
                _pendingEquipProjector = false;
            }

            while (_ecsCallbacks.TryDequeue(out _)) { }
            while (_pendingSaves.TryDequeue(out PendingSaveRequest staleSave))
            {
                staleSave.OnCompleted(Result<BlueprintSummary>.FromFailure("Not in game."));
            }

            _holdingProjector = false;
            DrainIgnoredQueues();
            _toolState.HidePreview();
            return;
        }

        _wasInGame = true;

        if (_pendingEquipProjector)
        {
            _pendingEquipProjector = false;
            SelectProjectorSlot(store);
        }

        while (_ecsCallbacks.TryDequeue(out Action? callback))
        {
            callback();
        }

        while (_pendingSaves.TryDequeue(out PendingSaveRequest saveReq))
        {
            ProcessSaveBlueprint(store, saveReq);
        }

        if (WaywardBeyond.GameState != GameState.Playing)
        {
            _holdingProjector = false;
            DrainIgnoredQueues();
            _toolState.HidePreview();
            return;
        }

        bool holding = IsHoldingProjector(store);
        _holdingProjector = holding;

        if (!holding || _interactionState.IsInteractionBlocked())
        {
            DrainIgnoredQueues();
            if (!holding)
            {
                _toolState.HidePreview();
            }

            return;
        }

        while (_pendingScrolls.TryDequeue(out float scroll))
        {
            if (_toolState.Mode == RegionToolMode.Paste)
            {
                _toolState.AdjustFreeSpaceDistance(scroll);
            }
        }

        while (_pendingActions.TryDequeue(out RegionToolAction action))
        {
            ExecuteAction(store, action);
        }

        while (_pendingClicks.TryDequeue(out MouseButton button))
        {
            ExecuteClick(store, button);
        }

        if (_toolState.Mode == RegionToolMode.Paste)
        {
            UpdatePastePlacement(store);
        }
        else
        {
            _toolState.HidePreview();
        }
    }

    private void RegisterShortcut(IShortcutService shortcutService, string name, Key key, ShortcutModifiers modifiers, Action handler)
    {
        shortcutService.RegisterShortcut(new Shortcut
        {
            Name = name,
            Category = "RegionTool",
            Modifiers = modifiers,
            Key = key,
            IsEnabled = IsShortcutEnabled,
            Action = handler,
        });
    }

    private bool IsShortcutEnabled()
    {
        return WaywardBeyond.GameState == GameState.Playing
            && _holdingProjector
            && !_interactionState.IsInteractionBlocked();
    }

    private void OnClicked(object? sender, ClickedEventArgs e)
    {
        if (WaywardBeyond.GameState != GameState.Playing || _interactionState.IsInteractionBlocked() || !_holdingProjector)
        {
            return;
        }

        if (e.MouseButton is MouseButton.Left or MouseButton.Right)
        {
            _pendingClicks.Enqueue(e.MouseButton);
        }
    }

    private void OnScrolled(object? sender, ScrolledEventArgs e)
    {
        if (WaywardBeyond.GameState != GameState.Playing
            || _interactionState.IsInteractionBlocked()
            || !_holdingProjector
            || !_inputService.IsKeyHeld(Key.Shift)
            || _toolState.Mode != RegionToolMode.Paste)
        {
            return;
        }

        double rounded = Math.Round(e.Delta, MidpointRounding.AwayFromZero);
        rounded = Math.Clamp(rounded, -4.0, 4.0);
        if (Math.Abs(rounded) > 0.01)
        {
            _pendingScrolls.Enqueue((float)(rounded * FREE_SPACE_SCROLL_STEP));
        }
    }

    private void ExecuteClick(DataStore store, MouseButton button)
    {
        if (!EnsureCreativeMode(store))
        {
            return;
        }

        if (_toolState.Mode == RegionToolMode.Paste)
        {
            if (button == MouseButton.Right)
            {
                _toolState.SetMode(RegionToolMode.Select);
                return;
            }

            if (button == MouseButton.Left)
            {
                ExecutePaste(store);
            }

            return;
        }

        if (!TryRaycastStructureCell(CharacterDefaults.REGION_TOOL_REACH, out Entity entity, out Int3 cell, out _, out _))
        {
            return;
        }

        Uuid entityUuid = store.GetUuid(entity.Ptr);
        if (button == MouseButton.Left)
        {
            _toolState.SetAnchorA(entityUuid, cell);
        }
        else if (button == MouseButton.Right)
        {
            if (!_toolState.SetAnchorB(entityUuid, cell))
            {
                NotifyAction(_formatter.GetString("notification.region.differentStructure"));
            }
        }
    }

    private void ExecuteAction(DataStore store, RegionToolAction action)
    {
        switch (action)
        {
            case RegionToolAction.SelectMode:
                _toolState.SetMode(RegionToolMode.Select);
                break;

            case RegionToolAction.Copy:
                if (EnsureCreativeMode(store))
                {
                    ExecuteCopy(store);
                }
                break;

            case RegionToolAction.Cut:
                if (EnsureCreativeMode(store))
                {
                    ExecuteCut(store);
                }
                break;

            case RegionToolAction.TogglePasteMode:
                if (EnsureCreativeMode(store) && !_toolState.TogglePasteMode())
                {
                    NotifyAction(_formatter.GetString("notification.region.noClipboard"));
                }
                break;

            case RegionToolAction.RotateYaw:
                if (_toolState.HasClipboard)
                {
                    _toolState.RotateYaw(1);
                }
                else
                {
                    NotifyAction(_formatter.GetString("notification.region.noClipboard"));
                }
                break;

            case RegionToolAction.RotatePitch:
                if (_toolState.HasClipboard)
                {
                    _toolState.RotatePitch(1);
                }
                else
                {
                    NotifyAction(_formatter.GetString("notification.region.noClipboard"));
                }
                break;

            case RegionToolAction.MirrorHorizontal:
                ApplyCameraRelativeMirror(Vector3.UnitX);
                break;

            case RegionToolAction.MirrorVertical:
                ApplyCameraRelativeMirror(Vector3.UnitY);
                break;

            case RegionToolAction.ToggleOverwriteAir:
            {
                bool enabled = _toolState.ToggleOverwriteAir();
                string key = enabled ? "notification.region.overwriteAirOn" : "notification.region.overwriteAirOff";
                NotifyAction(_formatter.GetString(key));
                break;
            }
        }
    }

    private void ApplyCameraRelativeMirror(Vector3 localCameraAxis)
    {
        if (!_toolState.HasClipboard)
        {
            NotifyAction(_formatter.GetString("notification.region.noClipboard"));
            return;
        }

        CameraEntity camera = _renderContext.MainCamera.Get();
        Vector3 cameraAxis = Vector3.Transform(localCameraAxis, camera.Transform.Orientation);

        Quaternion previewWorldOrientation = Quaternion.Identity;
        if (_toolState.TryGetPreviewPlacement(out _, out _, out _, out _, out _, out _, out Quaternion currentOrientation, out _))
        {
            previewWorldOrientation = currentOrientation;
        }

        Quaternion pasteFrame = previewWorldOrientation * _toolState.PasteRotation.ToQuaternion();
        MirrorAxes axis = PickClosestMirrorAxis(cameraAxis, pasteFrame);
        _toolState.ToggleMirror(axis);
    }

    private static MirrorAxes PickClosestMirrorAxis(Vector3 worldDirection, Quaternion pasteFrame)
    {
        float dotX = MathF.Abs(Vector3.Dot(worldDirection, Vector3.Transform(Vector3.UnitX, pasteFrame)));
        float dotY = MathF.Abs(Vector3.Dot(worldDirection, Vector3.Transform(Vector3.UnitY, pasteFrame)));
        float dotZ = MathF.Abs(Vector3.Dot(worldDirection, Vector3.Transform(Vector3.UnitZ, pasteFrame)));

        if (dotX >= dotY && dotX >= dotZ)
        {
            return MirrorAxes.X;
        }

        if (dotY >= dotZ)
        {
            return MirrorAxes.Y;
        }

        return MirrorAxes.Z;
    }

    private bool TryExtractCurrentSelection(DataStore store, out Uuid entityUuid, out Int3 min, out Int3 max, out VoxelRegionExtractResult extracted)
    {
        extracted = default;
        if (!_toolState.TryGetCompleteSelection(out entityUuid, out min, out max))
        {
            NotifyAction(_formatter.GetString("notification.region.invalidBounds"));
            return false;
        }

        if (!store.TryGet(entityUuid, out int entity) || !store.TryGet(entity, out VoxelComponent voxelComponent))
        {
            NotifyAction(_formatter.GetString("notification.region.invalidTarget"));
            return false;
        }

        extracted = VoxelRegionExtractor.Extract(voxelComponent.VoxelObject, min, max);
        if (extracted.BrickCount <= 0)
        {
            NotifyAction(_formatter.GetString("notification.region.empty"));
            return false;
        }

        return true;
    }

    private void ExecuteCopy(DataStore store)
    {
        if (!TryExtractCurrentSelection(store, out _, out _, out _, out VoxelRegionExtractResult extracted))
        {
            return;
        }

        VoxelRegionData copiedRegion = extracted.Region;
        _toolState.SetClipboard(in copiedRegion, extracted.BrickCount, enterPasteMode: false);
        string dims = $"{extracted.Region.SizeX}x{extracted.Region.SizeY}x{extracted.Region.SizeZ}";
        NotifyAction(_formatter.GetString("notification.region.copied", extracted.BrickCount, dims));
    }

    private void ExecuteCut(DataStore store)
    {
        if (_cutInFlight)
        {
            return;
        }

        if (!TryExtractCurrentSelection(store, out Uuid entityUuid, out Int3 min, out Int3 max, out VoxelRegionExtractResult extracted))
        {
            return;
        }

        VoxelRegionData cutRegion = extracted.Region;
        _toolState.SetClipboard(in cutRegion, extracted.BrickCount, enterPasteMode: false);
        _cutInFlight = true;
        int generation = _sessionGeneration;

        var request = new CutRegionRequest
        {
            TargetEntity = entityUuid.ToValue(),
            MinX = min.X,
            MinY = min.Y,
            MinZ = min.Z,
            MaxX = max.X,
            MaxY = max.Y,
            MaxZ = max.Z,
        };

        string dims = $"{extracted.Region.SizeX}x{extracted.Region.SizeY}x{extracted.Region.SizeZ}";
        _regionClient.CutRegionAsync(request).ContinueWith(task =>
        {
            _ecsCallbacks.Enqueue(() =>
            {
                if (generation != _sessionGeneration || !WaywardBeyond.IsInGame())
                {
                    return;
                }

                _cutInFlight = false;
                if (task.IsFaulted || task.IsCanceled)
                {
                    NotifyAction(_formatter.GetString("notification.region.invalidTarget"));
                    return;
                }

                RpcResult<RegionOpResponse> result = task.Result;
                if (!result.IsSuccess)
                {
                    string key = result.ErrorCode == GameRpcErrors.PERMISSION_DENIED
                        ? "notification.region.permissionDenied"
                        : "notification.region.invalidTarget";
                    NotifyAction(_formatter.GetString(key));
                    return;
                }

                if (!result.Value.Success)
                {
                    string errKey = string.IsNullOrEmpty(result.Value.ErrorKey)
                        ? "notification.region.invalidTarget"
                        : result.Value.ErrorKey!;
                    NotifyAction(_formatter.GetString(errKey));
                    return;
                }

                _toolState.ClearSelection();
                NotifyAction(_formatter.GetString("notification.region.cut", result.Value.AffectedBricks, dims));
            });
        });
    }

    private void UpdatePastePlacement(DataStore store)
    {
        if (!_toolState.TryGetTransformedClipboard(_brickDatabase, out VoxelRegionData region, out int revision, out Orientation rotation, out MirrorAxes mirror))
        {
            _toolState.HidePreview();
            return;
        }

        if (!TryGetPlayerPosition(store, out Vector3 playerPos))
        {
            _toolState.HidePreview();
            return;
        }

        var regionSize = new Int3(region.SizeX, region.SizeY, region.SizeZ);
        if (TryRaycastStructureCell(CharacterDefaults.REGION_TOOL_REACH, out Entity entity, out Int3 hitCell, out Vector3 worldNormal, out TransformComponent targetTransform))
        {
            Vector3 localNormal = Vector3.Transform(worldNormal, Quaternion.Inverse(targetTransform.Orientation));
            bool embed = _inputService.IsKeyHeld(Key.Shift);
            Int3 origin = RegionPlacementResolver.ComputeFlushOrigin(hitCell, localNormal, regionSize, embed);

            bool boundsValid = RegionLimits.TryComputePasteMax(origin, regionSize.X, regionSize.Y, regionSize.Z, out Int3 maxCell)
                && (!entity.TryGet(out VoxelComponent targetVoxelComp)
                    || RegionLimits.IsWithinChunkRange(origin, maxCell, targetVoxelComp.VoxelObject.ChunkSize));
            bool inReach = boundsValid
                && RegionPlacementResolver.IsBoxWithinReach(origin, maxCell, playerPos, in targetTransform, CharacterDefaults.REGION_PASTE_MAX_REACH);
            bool overlaps = inReach && OverlapsWorldCached(revision, rotation, mirror, in region, entity.Ptr, in targetTransform, origin);
            string? errorKey = !boundsValid
                ? "notification.region.invalidBounds"
                : (!inReach ? "notification.region.outOfReach" : (overlaps ? "notification.placement.occupied" : null));

            Vector3 worldPos = targetTransform.Position + Vector3.Transform(new Vector3(origin.X, origin.Y, origin.Z) * targetTransform.Scale, targetTransform.Orientation);
            _toolState.SetPreviewPlacement(
                visible: true,
                valid: errorKey == null,
                PasteTargetKind.IntoStructure,
                store.GetUuid(entity.Ptr),
                origin,
                regionSize,
                worldPos,
                targetTransform.Orientation,
                Vector3.Zero,
                errorKey,
                targetTransform.Scale);
            return;
        }

        CameraEntity camera = _renderContext.MainCamera.Get();
        RegionPlacementResolver.ComputeFreeSpacePose(
            camera.Transform.Position,
            camera.Transform.Orientation,
            _toolState.FreeSpaceDistance,
            regionSize,
            out Vector3 spawnPos,
            out Quaternion spawnRot);

        var centerOffset = new Int3(-(regionSize.X / 2), -(regionSize.Y / 2), -(regionSize.Z / 2));
        var localOffset = new Vector3(centerOffset.X, centerOffset.Y, centerOffset.Z);
        var spawnTransform = new TransformComponent(spawnPos, spawnRot, Vector3.One);

        bool spawnInReach = RegionPlacementResolver.IsPointWithinReach(spawnPos, playerPos, CharacterDefaults.REGION_PASTE_MAX_REACH);
        bool spawnOverlaps = spawnInReach && OverlapsWorldCached(revision, rotation, mirror, in region, ignoredEntity: -1, in spawnTransform, centerOffset);
        string? spawnErrorKey = !spawnInReach
            ? "notification.region.outOfReach"
            : (spawnOverlaps ? "notification.placement.occupied" : null);

        _toolState.SetPreviewPlacement(
            visible: true,
            valid: spawnErrorKey == null,
            PasteTargetKind.NewStructure,
            Uuid.Null,
            centerOffset,
            regionSize,
            spawnPos,
            spawnRot,
            localOffset,
            spawnErrorKey);
    }

    private void ExecutePaste(DataStore store)
    {
        if (_pasteInFlight)
        {
            return;
        }

        UpdatePastePlacement(store);
        if (!_toolState.TryGetPreviewPlacement(
                out bool valid,
                out PasteTargetKind targetKind,
                out Uuid targetEntity,
                out Int3 origin,
                out _,
                out Vector3 worldPos,
                out Quaternion worldRot,
                out _))
        {
            NotifyAction(_formatter.GetString("notification.region.noClipboard"));
            return;
        }

        if (!valid)
        {
            string invalidKey = _toolState.PreviewInvalidErrorKey;
            NotifyAction(_formatter.GetString(
                string.IsNullOrEmpty(invalidKey) ? "notification.placement.occupied" : invalidKey));
            return;
        }

        if (!_toolState.TryGetTransformedClipboard(_brickDatabase, out VoxelRegionData region, out _, out _, out _))
        {
            return;
        }

        VoxelRegionData encoded = VoxelCodec.Encode(in region, _brickRegistry, _paintRegistry);
        var request = new PasteRegionRequest
        {
            TargetKind = targetKind,
            TargetEntity = targetEntity.ToValue(),
            OriginX = origin.X,
            OriginY = origin.Y,
            OriginZ = origin.Z,
            SpawnX = worldPos.X,
            SpawnY = worldPos.Y,
            SpawnZ = worldPos.Z,
            SpawnOrientationX = worldRot.X,
            SpawnOrientationY = worldRot.Y,
            SpawnOrientationZ = worldRot.Z,
            SpawnOrientationW = worldRot.W,
            OverwriteAir = _toolState.OverwriteAir,
            SizeX = encoded.SizeX,
            SizeY = encoded.SizeY,
            SizeZ = encoded.SizeZ,
            Chunks = encoded.Chunks,
            BrickPalette = encoded.BrickPalette,
            PaintPalette = encoded.PaintPalette,
        };

        _pasteInFlight = true;
        int generation = _sessionGeneration;
        PasteTargetKind sentKind = targetKind;
        _regionClient.PasteRegionAsync(request).ContinueWith(task =>
        {
            _ecsCallbacks.Enqueue(() =>
            {
                if (generation != _sessionGeneration || !WaywardBeyond.IsInGame())
                {
                    return;
                }

                _pasteInFlight = false;
                if (task.IsFaulted || task.IsCanceled)
                {
                    NotifyAction(_formatter.GetString("notification.region.invalidTarget"));
                    return;
                }

                RpcResult<RegionOpResponse> result = task.Result;
                if (!result.IsSuccess)
                {
                    string key = result.ErrorCode == GameRpcErrors.PERMISSION_DENIED
                        ? "notification.region.permissionDenied"
                        : "notification.region.invalidTarget";
                    NotifyAction(_formatter.GetString(key));
                    return;
                }

                if (!result.Value.Success)
                {
                    string errKey = string.IsNullOrEmpty(result.Value.ErrorKey)
                        ? "notification.region.invalidTarget"
                        : result.Value.ErrorKey!;
                    NotifyAction(_formatter.GetString(errKey));
                    return;
                }

                if (result.Value.SkippedUnknownBricks > 0)
                {
                    _notificationService.Push(new Notification(_formatter.GetString("notification.region.pasteSkippedUnknown", result.Value.SkippedUnknownBricks)));
                }

                string okKey = sentKind == PasteTargetKind.NewStructure
                    ? "notification.region.spawned"
                    : "notification.region.pasted";
                NotifyAction(_formatter.GetString(okKey, result.Value.AffectedBricks));
            });
        });
    }

    private void ProcessSaveBlueprint(DataStore store, in PendingSaveRequest saveReq)
    {
        VoxelRegionData sourceRegion;
        int brickCount;

        if (_toolState.TryGetCompleteSelection(out _, out _, out _)
            && TryExtractCurrentSelection(store, out _, out _, out _, out VoxelRegionExtractResult extracted))
        {
            sourceRegion = extracted.Region;
            brickCount = extracted.BrickCount;
        }
        else if (_toolState.TryGetRawClipboard(out sourceRegion, out brickCount, out _))
        {
        }
        else
        {
            Result<BlueprintSummary> noSource = Result<BlueprintSummary>.FromFailure(_formatter.GetString("notification.region.noClipboard"));
            NotifyAction(noSource.Message!);
            saveReq.OnCompleted(noSource);
            return;
        }

        VoxelRegionData encoded = VoxelCodec.Encode(in sourceRegion, _brickRegistry, _paintRegistry);
        Result<BlueprintSummary> saved = _blueprintStorage.Save(saveReq.Name, in encoded, brickCount);
        if (saved.Success)
        {
            _notificationService.Push(new Notification(_formatter.GetString("notification.blueprint.saved", saved.Value.Name)));
        }
        else
        {
            _notificationService.Push(new Notification(_formatter.GetString("notification.blueprint.saveFailed")));
        }

        saveReq.OnCompleted(saved);
    }

    private bool OverlapsWorldCached(
        int revision,
        Orientation rotation,
        MirrorAxes mirror,
        in VoxelRegionData region,
        int ignoredEntity,
        in TransformComponent targetTransform,
        Int3 origin
    )
    {
        if (_overlapCacheRevision != revision || _overlapCacheRotation != rotation || _overlapCacheMirror != mirror)
        {
            var tempObj = new VoxelObject(VoxelObject.DEFAULT_CHUNK_EDGE);
            VoxelRegionApplier.ApplyTo(tempObj, in region, default, overwriteAir: false);
            _overlapCacheShape = VoxelCollision.BuildCompound(tempObj, _brickDatabase);
            _overlapCacheRevision = revision;
            _overlapCacheRotation = rotation;
            _overlapCacheMirror = mirror;
        }

        if (_overlapCacheShape.Shapes.Length == 0)
        {
            return false;
        }

        var originVec = new Vector3(origin.X, origin.Y, origin.Z);
        for (var i = 0; i < _overlapCacheShape.Shapes.Length; i++)
        {
            Vector3 localPos = (_overlapCacheShape.Positions[i] + originVec) * targetTransform.Scale;
            Vector3 worldPos = Vector3.Transform(localPos, targetTransform.Orientation) + targetTransform.Position;
            Quaternion worldRot = targetTransform.Orientation * _overlapCacheShape.Orientations[i];

            var query = new ShapeOverlapQuery(
                _overlapCacheShape.Shapes[i],
                worldPos,
                worldRot,
                targetTransform.Scale,
                ignoredEntity: ignoredEntity,
                minPenetration: OVERLAP_PENETRATION_EPSILON);

            if (_physics.Overlaps(in query))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryRaycastStructureCell(
        float reach,
        out Entity entity,
        out Int3 hitCell,
        out Vector3 hitNormal,
        out TransformComponent transform
    )
    {
        CameraEntity camera = _renderContext.MainCamera.Get();
        Ray ray = camera.ScreenPointToRay(
            (int)_windowContext.Resolution.X / 2,
            (int)_windowContext.Resolution.Y / 2,
            (int)_windowContext.Resolution.X,
            (int)_windowContext.Resolution.Y);

        ray = new Ray(ray.Origin + RAYCAST_ORIGIN_OFFSET * ray.Vector, ray.Vector * (reach - RAYCAST_ORIGIN_OFFSET));
        RaycastResult raycast = _physics.Raycast(ray);
        if (raycast.Hit && raycast.Entity.TryGet(out VoxelComponent _) && raycast.Entity.TryGet(out transform))
        {
            entity = raycast.Entity;
            hitNormal = raycast.Normal;
            hitCell = InteractionResolver.ResolveHitCell(
                raycast.Point,
                raycast.Normal,
                transform.Position,
                transform.Orientation);
            return true;
        }

        entity = default;
        hitCell = default;
        hitNormal = default;
        transform = default;
        return false;
    }

    private bool EnsureCreativeMode(DataStore store)
    {
        GameMode mode = GameMode.Adventure;
        store.Query<PlayerComponent, GameModeComponent>(
            0f,
            (float _, DataStore _, int _, in PlayerComponent _, in GameModeComponent gm) => mode = gm.Mode);
        if (mode == GameMode.Creative)
        {
            return true;
        }

        NotifyAction(_formatter.GetString("notification.region.creativeOnly"));
        return false;
    }

    private void NotifyAction(string message)
    {
        _notificationService.Push(new Notification(message, NotificationType.Action));
    }

    private bool IsHoldingProjector(DataStore store)
    {
        Result<ItemSlot> mainHand = _playerData.GetMainHand(store);
        return mainHand.Success && mainHand.Value.Item.Tool?.Type == ToolType.Blueprint;
    }

    private void SelectProjectorSlot(DataStore store)
    {
        Result<InventoryComponent> invResult = _playerData.GetInventory(store);
        if (!invResult.Success || invResult.Value.Contents == null)
        {
            return;
        }

        ItemData[] contents = invResult.Value.Contents;
        int limit = Math.Min(9, contents.Length);
        for (var i = 0; i < limit; i++)
        {
            if (contents[i].Count > 0 && string.Equals(contents[i].ID, "wb:projector", StringComparison.Ordinal))
            {
                _playerData.SetActiveSlot(store, i);
                return;
            }
        }
    }

    private static bool TryGetPlayerPosition(DataStore store, out Vector3 position)
    {
        var found = false;
        Vector3 pos = Vector3.Zero;
        store.Query<PlayerComponent, TransformComponent>(
            0f,
            (float _, DataStore _, int _, in PlayerComponent _, in TransformComponent t) =>
            {
                pos = t.Position;
                found = true;
            });
        position = pos;
        return found;
    }

    private void DrainIgnoredQueues()
    {
        while (_pendingClicks.TryDequeue(out _)) { }
        while (_pendingScrolls.TryDequeue(out _)) { }
        while (_pendingActions.TryDequeue(out _)) { }
    }

    private readonly struct PendingSaveRequest(string name, Action<Result<BlueprintSummary>> onCompleted)
    {
        public readonly string Name = name;
        public readonly Action<Result<BlueprintSummary>> OnCompleted = onCompleted;
    }
}
