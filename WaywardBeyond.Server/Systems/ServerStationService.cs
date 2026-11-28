using System;
using Krill;
using Microsoft.Extensions.Logging;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Statistics;
using WaywardBeyond.Gameplay.Components;
using WaywardBeyond.Gameplay.Stations;
using WaywardBeyond.Server.Permissions;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// Per-world authoritative manager for player <see cref="ActiveStationComponent"/> bindings, viewer
/// subscriptions, station authorization, and automatic teardown on reach loss, brick destruction,
/// player death, or disconnect.
/// </summary>
public sealed class ServerStationService : IStationSession
{
    private readonly ServerBrickEntityService _brickEntities;
    private readonly NetworkReplicationSystem? _replication;
    private readonly SessionManager? _sessions;
    private readonly IUserPermissionService? _permissions;
    private readonly ILogger<ServerStationService>? _logger;

    /// <summary>Creates a per-world station lifecycle service.</summary>
    public ServerStationService(
        ServerBrickEntityService brickEntities,
        NetworkReplicationSystem? replication = null,
        SessionManager? sessions = null,
        IUserPermissionService? permissions = null,
        ILogger<ServerStationService>? logger = null
    )
    {
        _brickEntities = brickEntities ?? throw new ArgumentNullException(nameof(brickEntities));
        _replication = replication;
        _sessions = sessions;
        _permissions = permissions;
        _logger = logger;

        _brickEntities.BrickEntityDestroyed += OnBrickEntityDestroyed;
    }

    /// <inheritdoc/>
    public Result OpenBrickStation(DataStore store, int playerEntity, Uuid clientId, Uuid voxelEntityUuid, Int3 voxelCoord)
    {
        if (playerEntity == Entity.Null || voxelEntityUuid == Uuid.Null)
        {
            return Result.FromFailure("Invalid player or voxel structure.");
        }

        if (IsPlayerDead(store, playerEntity))
        {
            return Result.FromFailure("Dead players cannot open stations.");
        }

        if (!_brickEntities.TryGetEntity(voxelEntityUuid, voxelCoord, out int brickEntity))
        {
            return Result.FromFailure("Target brick has no active brick-entity.");
        }

        string stationTag = ResolveStationTag(store, brickEntity);
        bool hasInventory = store.Has(typeof(InventoryComponent), brickEntity);
        StationUiKind uiKind = StationTags.ResolveUiKind(stationTag, hasInventory);
        if (uiKind == StationUiKind.None)
        {
            return Result.FromFailure("Target brick-entity does not open a station interface.");
        }

        return OpenStation(store, playerEntity, clientId, brickEntity, voxelEntityUuid, voxelCoord, stationTag, uiKind);
    }

    /// <inheritdoc/>
    public Result OpenStation(
        DataStore store,
        int playerEntity,
        Uuid clientId,
        int stationEntity,
        Uuid voxelEntityUuid,
        Int3 voxelCoord,
        string stationTag,
        StationUiKind uiKind
    )
    {
        if (playerEntity == Entity.Null || stationEntity == Entity.Null || voxelEntityUuid == Uuid.Null)
        {
            return Result.FromFailure("Invalid station open request.");
        }

        if (!Enum.IsDefined(uiKind) || uiKind == StationUiKind.None)
        {
            return Result.FromFailure("Station UI kind must not be None.");
        }

        if (IsPlayerDead(store, playerEntity))
        {
            return Result.FromFailure("Dead players cannot open stations.");
        }

        Uuid stationUuid = store.GetUuid(stationEntity);
        if (stationUuid == Uuid.Null)
        {
            return Result.FromFailure("Target station entity is not live.");
        }

        Result viewerResult = AttachOrValidateViewer(store, playerEntity, clientId, stationEntity);
        if (!viewerResult.Success)
        {
            return viewerResult;
        }

        bool hasExisting = store.TryGet(playerEntity, out ActiveStationComponent existing);
        if (hasExisting
            && existing.IsOpen
            && existing.StationEntity != stationUuid.ToValue()
            && clientId != Uuid.Null
            && store.TryGet(Uuid.FromValue(existing.StationEntity), out int previousEntity))
        {
            _brickEntities.DetachViewer(store, clientId, previousEntity);
        }

        var next = new ActiveStationComponent(
            stationUuid.ToValue(),
            voxelEntityUuid.ToValue(),
            voxelCoord.X,
            voxelCoord.Y,
            voxelCoord.Z,
            stationTag,
            uiKind);

        if (!hasExisting || !existing.EqualsState(in next))
        {
            store.AddOrUpdate(playerEntity, next);
        }

        _logger?.LogDebug(
            "Opened station \"{tag}\" ({stationUuid}, {uiKind}) for player {player}.",
            next.StationTag, stationUuid, uiKind, playerEntity);
        return Result.FromSuccess();
    }

    /// <inheritdoc/>
    public bool CloseStation(DataStore store, int playerEntity, Uuid clientId)
    {
        if (playerEntity == Entity.Null
            || !store.TryGet(playerEntity, out ActiveStationComponent active)
            || !active.IsOpen)
        {
            return false;
        }

        if (clientId != Uuid.Null
            && store.TryGet(Uuid.FromValue(active.StationEntity), out int stationEntity))
        {
            _brickEntities.DetachViewer(store, clientId, stationEntity);
        }

        store.AddOrUpdate(playerEntity, ActiveStationComponent.None);
        _logger?.LogDebug("Closed active station {stationUuid} for player {player}.", active.StationEntity, playerEntity);
        return true;
    }

    /// <inheritdoc/>
    public Result<int> AuthorizeBrickEntity(DataStore store, int playerEntity, Uuid clientId, Uuid targetUuid)
    {
        if (playerEntity == Entity.Null || targetUuid == Uuid.Null)
        {
            return Result<int>.FromFailure("Invalid player or target entity.");
        }

        if (IsPlayerDead(store, playerEntity))
        {
            return Result<int>.FromFailure("Dead players cannot operate stations.");
        }

        if (!store.TryGet(targetUuid, out int targetEntity))
        {
            return Result<int>.FromFailure("Target brick-entity does not exist.");
        }

        if (!store.TryGet(targetEntity, out BrickAnchorComponent anchor)
            || !_brickEntities.TryGetEntity(in anchor, out int indexed)
            || indexed != targetEntity)
        {
            return Result<int>.FromFailure("Target entity is not an active brick-entity.");
        }

        Result access = _brickEntities.ValidateViewerAccess(store, clientId, playerEntity, targetEntity, in anchor);
        if (!access.Success)
        {
            return Result<int>.FromFailure(access.Message);
        }

        return Result<int>.FromSuccess(targetEntity);
    }

    /// <inheritdoc/>
    public bool HasPermission(Uuid clientId, string permission)
    {
        return clientId != Uuid.Null
            && !string.IsNullOrEmpty(permission)
            && _permissions != null
            && _permissions.HasPermission(clientId, permission);
    }

    /// <inheritdoc/>
    public bool IsPlayerDead(DataStore store, int playerEntity)
    {
        if (playerEntity == Entity.Null)
        {
            return false;
        }

        if (store.TryGet(playerEntity, out GameModeComponent mode) && mode.Mode == GameMode.Creative)
        {
            return false;
        }

        if (store.TryGet(playerEntity, out StatisticsComponent stats)
            && stats.TryGetValue(StatisticIds.HEALTH, out long healthFixed)
            && healthFixed <= 0L)
        {
            return true;
        }

        return false;
    }

    /// <summary>Closes any open station held by a disconnecting or leaving client before its mirror is freed.</summary>
    public void OnClientDisconnected(DataStore store, Uuid clientId, int playerEntity)
    {
        CloseStation(store, playerEntity, clientId);
    }

    /// <summary>
    /// Validates every open <see cref="ActiveStationComponent"/> in <paramref name="store"/>, closing any
    /// station whose player moved out of reach, died, disconnected, lost access, or whose brick was destroyed.
    /// </summary>
    public void ReconcileActiveStations(DataStore store)
    {
        ReconcileAction action = new() { Owner = this };
        store.QueryRef<ActiveStationComponent, ReconcileAction>(0f, ref action);
    }

    private Result AttachOrValidateViewer(DataStore store, int playerEntity, Uuid clientId, int stationEntity)
    {
        if (_replication != null)
        {
            return _brickEntities.AttachViewer(store, clientId, stationEntity);
        }

        if (!store.TryGet(stationEntity, out BrickAnchorComponent anchor)
            || !_brickEntities.TryGetEntity(in anchor, out int indexed)
            || indexed != stationEntity)
        {
            return Result.FromFailure("Target entity is not an active brick-entity.");
        }

        return _brickEntities.ValidateViewerAccess(store, clientId, playerEntity, stationEntity, in anchor);
    }

    private string ResolveStationTag(DataStore store, int brickEntity)
    {
        if (store.TryGet(brickEntity, out MachineStateComponent machine)
            && !string.IsNullOrEmpty(machine.StationTag))
        {
            return machine.StationTag;
        }

        if (_brickEntities.TryGetBrick(brickEntity, out Brick? brick)
            && brick != null
            && !string.IsNullOrEmpty(brick.StationTag))
        {
            return brick.StationTag;
        }

        return string.Empty;
    }

    private void OnBrickEntityDestroyed(DataStore store, int brickEntity, Uuid brickEntityUuid)
    {
        ulong rawUuid = brickEntityUuid.ToValue();
        if (rawUuid == 0UL)
        {
            return;
        }

        var action = new CloseDestroyedStationAction(rawUuid);
        store.QueryRef<ActiveStationComponent, CloseDestroyedStationAction>(0f, ref action);
    }

    private bool IsStationStillValid(
        DataStore store,
        int playerEntity,
        in ActiveStationComponent active,
        out Uuid clientId,
        out int stationEntity
    )
    {
        clientId = Uuid.Null;
        stationEntity = Entity.Null;

        bool hasSession = _sessions == null || _sessions.TryGetClient(playerEntity, out clientId);
        Uuid stationUuid = Uuid.FromValue(active.StationEntity);
        bool hasStationEntity = stationUuid != Uuid.Null && store.TryGet(stationUuid, out stationEntity);
        if (!hasStationEntity)
        {
            stationEntity = Entity.Null;
        }

        if (!hasSession || !hasStationEntity || IsPlayerDead(store, playerEntity))
        {
            return false;
        }

        if (!store.TryGet(stationEntity, out BrickAnchorComponent anchor)
            || anchor.VoxelEntityUuid.ToValue() != active.VoxelEntity
            || anchor.VoxelCoord.X != active.VoxelX
            || anchor.VoxelCoord.Y != active.VoxelY
            || anchor.VoxelCoord.Z != active.VoxelZ
            || !_brickEntities.TryGetEntity(in anchor, out int indexed)
            || indexed != stationEntity)
        {
            return false;
        }

        if (_replication != null
            && clientId != Uuid.Null
            && !_replication.IsViewerSubscribed(stationEntity, clientId))
        {
            return false;
        }

        return _brickEntities.ValidateViewerAccess(store, clientId, playerEntity, stationEntity, in anchor).Success;
    }

    private struct ReconcileAction : IForEachRef<ActiveStationComponent>
    {
        public ServerStationService Owner;

        public void Execute(float delta, DataStore store, int entity, ref Ref<ActiveStationComponent> active)
        {
            ref readonly ActiveStationComponent current = ref active.Read;
            if (!current.IsOpen)
            {
                return;
            }

            if (Owner.IsStationStillValid(store, entity, in current, out Uuid clientId, out int stationEntity))
            {
                return;
            }

            if (clientId != Uuid.Null && stationEntity != Entity.Null)
            {
                Owner._brickEntities.DetachViewer(store, clientId, stationEntity);
            }

            active.Write = ActiveStationComponent.None;
        }
    }

    private readonly struct CloseDestroyedStationAction(ulong destroyedEntityUuid) : IForEachRef<ActiveStationComponent>
    {
        private readonly ulong _destroyedEntityUuid = destroyedEntityUuid;

        public void Execute(float delta, DataStore store, int entity, ref Ref<ActiveStationComponent> active)
        {
            ref readonly ActiveStationComponent current = ref active.Read;
            if (current.IsOpen && current.StationEntity == _destroyedEntityUuid)
            {
                active.Write = ActiveStationComponent.None;
            }
        }
    }
}
