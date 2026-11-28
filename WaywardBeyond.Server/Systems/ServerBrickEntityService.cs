using System;
using System.Collections.Generic;
using System.Numerics;
using Krill;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Levels;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Components;
using WaywardBeyond.Gameplay.Networking;
using WaywardBeyond.Items;
using WaywardBeyond.Server.Components;
using WaywardBeyond.Server.Permissions;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// Per-world authoritative manager for server ECS brick-entities anchored to bricks claimed by
/// <see cref="IServerBrickEntityDecorator"/> hooks. Maintains the <c>(VoxelEntityUuid, Int3) -> Entity</c>
/// lookup index, handles placement, break, region/blueprint sync, viewer attachment and reach eviction,
/// programmatic cargo seeding, and <c>level.db</c> capture/restore.
/// </summary>
public sealed class ServerBrickEntityService
{
    private readonly IBrickDatabase _brickDatabase;
    private readonly NetworkReplicationSystem? _replication;
    private readonly SessionManager? _sessions;
    private readonly IItemDatabase? _itemDatabase;
    private readonly IUserPermissionService? _permissions;
    private readonly ILogger<ServerBrickEntityService>? _logger;
    private readonly IServerBrickEntityDecorator[] _decorators;

    private readonly Dictionary<BrickAnchorComponent, int> _byAnchor = [];
    private readonly Dictionary<Uuid, HashSet<BrickAnchorComponent>> _anchorsByStructure = [];
    private readonly Dictionary<int, Brick> _brickByEntity = [];
    private readonly List<(Uuid ClientId, int Entity)> _subscriptionScratch = [];
    private readonly List<BrickAnchorComponent> _anchorScratch = [];

    /// <summary>Constructs a per-world server brick-entity service.</summary>
    public ServerBrickEntityService(
        IBrickDatabase brickDatabase,
        NetworkReplicationSystem? replication = null,
        SessionManager? sessions = null,
        ISlotGroupSchemaDatabase? schemas = null,
        IItemDatabase? itemDatabase = null,
        IUserPermissionService? permissions = null,
        ILogger<ServerBrickEntityService>? logger = null,
        IServerBrickEntityDecorator[]? decorators = null
    )
    {
        _brickDatabase = brickDatabase;
        _replication = replication;
        _sessions = sessions;
        _itemDatabase = itemDatabase;
        _permissions = permissions;
        _logger = logger;
        _decorators = decorators is { Length: > 0 }
            ? decorators
            : [
                new InventoryBrickEntityDecorator(schemas, itemDatabase),
                new MachineBrickEntityDecorator(),
            ];
    }

    /// <summary>Raised when a live brick-entity is destroyed, immediately before its ECS entity is freed.</summary>
    internal event Action<DataStore, int, Uuid>? BrickEntityDestroyed;

    /// <summary>The number of live server brick-entities tracked by this world.</summary>
    public int Count => _byAnchor.Count;

    /// <summary>Returns whether any registered server decorator applies to <paramref name="brick"/>.</summary>
    public bool IsServerBrickEntity(Brick brick)
    {
        for (var i = 0; i < _decorators.Length; i++)
        {
            if (_decorators[i].AppliesTo(brick))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Attempts to resolve the server ECS brick-entity anchored at <paramref name="anchor"/>.</summary>
    public bool TryGetEntity(in BrickAnchorComponent anchor, out int entity)
    {
        return _byAnchor.TryGetValue(anchor, out entity);
    }

    /// <summary>Attempts to resolve the server ECS brick-entity anchored at (<paramref name="structureUuid"/>, <paramref name="coord"/>).</summary>
    public bool TryGetEntity(Uuid structureUuid, Int3 coord, out int entity)
    {
        return _byAnchor.TryGetValue(new BrickAnchorComponent(structureUuid, coord), out entity);
    }

    /// <summary>Attempts to resolve the <see cref="Brick"/> definition associated with <paramref name="entity"/>.</summary>
    internal bool TryGetBrick(int entity, out Brick? brick)
    {
        return _brickByEntity.TryGetValue(entity, out brick);
    }

    /// <summary>Gets all live brick-entity ids anchored to <paramref name="structureUuid"/>.</summary>
    public int[] GetEntitiesForStructure(Uuid structureUuid)
    {
        if (!_anchorsByStructure.TryGetValue(structureUuid, out HashSet<BrickAnchorComponent>? anchors) || anchors.Count == 0)
        {
            return [];
        }

        var entities = new int[anchors.Count];
        var index = 0;
        foreach (BrickAnchorComponent anchor in anchors)
        {
            if (_byAnchor.TryGetValue(anchor, out int entity))
            {
                entities[index++] = entity;
            }
        }

        if (index == entities.Length)
        {
            return entities;
        }

        var trimmed = new int[index];
        Array.Copy(entities, trimmed, index);
        return trimmed;
    }

    /// <summary>Attempts to find the first brick-entity on <paramref name="structureUuid"/> matching <paramref name="stationTag"/>.</summary>
    public bool TryFindFirstByStationTag(DataStore store, Uuid structureUuid, string stationTag, out int entity)
    {
        entity = Entity.Null;
        if (string.IsNullOrEmpty(stationTag)
            || !_anchorsByStructure.TryGetValue(structureUuid, out HashSet<BrickAnchorComponent>? anchors))
        {
            return false;
        }

        foreach (BrickAnchorComponent anchor in anchors)
        {
            if (_byAnchor.TryGetValue(anchor, out int candidate)
                && store.TryGet(candidate, out MachineStateComponent machine)
                && string.Equals(machine.StationTag, stationTag, StringComparison.Ordinal))
            {
                entity = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>Computes the live world-space position of the brick-entity anchored by <paramref name="anchor"/>.</summary>
    public static bool TryResolveWorldPosition(DataStore store, in BrickAnchorComponent anchor, out Vector3 worldPosition)
    {
        worldPosition = default;
        if (anchor.VoxelEntityUuid == Uuid.Null
            || !store.TryGet(anchor.VoxelEntityUuid, out int structureEntity)
            || !store.TryGet(structureEntity, out TransformComponent structureTransform)
            || !IsFinite(structureTransform.Position)
            || !IsFinite(structureTransform.Scale)
            || !IsFinite(structureTransform.Orientation))
        {
            return false;
        }

        var localPos = new Vector3(anchor.VoxelCoord.X, anchor.VoxelCoord.Y, anchor.VoxelCoord.Z);
        Vector3 position = Vector3.Transform(localPos * structureTransform.Scale, structureTransform.Orientation)
            + structureTransform.Position;
        if (!IsFinite(position))
        {
            return false;
        }

        worldPosition = position;
        return true;
    }

    /// <summary>
    /// Allocates or updates the server brick-entity when a brick is placed at (<paramref name="structureUuid"/>, <paramref name="coord"/>).
    /// Returns the allocated entity id, or <see cref="Entity.Null"/> when no server decorator applies to the brick.
    /// </summary>
    public int OnBrickPlaced(DataStore store, Uuid structureUuid, Int3 coord, ushort brickDataId)
    {
        if (structureUuid == Uuid.Null || brickDataId == 0)
        {
            return Entity.Null;
        }

        Result<Brick> lookup = _brickDatabase.Get(brickDataId);
        if (!lookup.Success || !IsServerBrickEntity(lookup.Value))
        {
            RemoveAt(store, new BrickAnchorComponent(structureUuid, coord));
            return Entity.Null;
        }

        return SpawnBrickEntity(store, structureUuid, coord, lookup.Value, persisted: null);
    }

    /// <summary>
    /// Handles breaking a brick with a server entity: transfers stored contents to <paramref name="breakingPlayer"/>
    /// in non-Creative modes, tears down active viewer sessions, and frees the brick-entity.
    /// </summary>
    public bool OnBrickBroken(
        DataStore store,
        Uuid structureUuid,
        Int3 coord,
        int breakingPlayer = Entity.Null,
        GameMode gameMode = GameMode.Adventure
    )
    {
        var anchor = new BrickAnchorComponent(structureUuid, coord);
        if (!_byAnchor.TryGetValue(anchor, out int brickEntity))
        {
            return false;
        }

        if (gameMode != GameMode.Creative
            && breakingPlayer != Entity.Null
            && store.TryGet(brickEntity, out InventoryComponent brickInventory))
        {
            TransferContentsToPlayer(store, breakingPlayer, in brickInventory);
        }

        DestroyBrickEntity(store, anchor, brickEntity);
        return true;
    }

    /// <summary>Removes and frees every brick-entity anchored to <paramref name="structureUuid"/>.</summary>
    public int OnStructureRemoved(DataStore store, Uuid structureUuid)
    {
        if (!_anchorsByStructure.TryGetValue(structureUuid, out HashSet<BrickAnchorComponent>? anchors) || anchors.Count == 0)
        {
            return 0;
        }

        _anchorScratch.Clear();
        foreach (BrickAnchorComponent anchor in anchors)
        {
            _anchorScratch.Add(anchor);
        }

        var removed = 0;
        for (var i = 0; i < _anchorScratch.Count; i++)
        {
            BrickAnchorComponent anchor = _anchorScratch[i];
            if (_byAnchor.TryGetValue(anchor, out int brickEntity))
            {
                DestroyBrickEntity(store, anchor, brickEntity);
                removed++;
            }
        }

        _anchorScratch.Clear();
        _anchorsByStructure.Remove(structureUuid);
        return removed;
    }

    /// <summary>Removes and frees every brick-entity on <paramref name="structureUuid"/> inside [<paramref name="min"/>, <paramref name="max"/>].</summary>
    public int OnRegionCleared(DataStore store, Uuid structureUuid, Int3 min, Int3 max)
    {
        if (!_anchorsByStructure.TryGetValue(structureUuid, out HashSet<BrickAnchorComponent>? anchors) || anchors.Count == 0)
        {
            return 0;
        }

        _anchorScratch.Clear();
        foreach (BrickAnchorComponent anchor in anchors)
        {
            Int3 c = anchor.VoxelCoord;
            if (c.X >= min.X && c.X <= max.X
                && c.Y >= min.Y && c.Y <= max.Y
                && c.Z >= min.Z && c.Z <= max.Z)
            {
                _anchorScratch.Add(anchor);
            }
        }

        var removed = 0;
        for (var i = 0; i < _anchorScratch.Count; i++)
        {
            BrickAnchorComponent anchor = _anchorScratch[i];
            if (_byAnchor.TryGetValue(anchor, out int brickEntity))
            {
                DestroyBrickEntity(store, anchor, brickEntity);
                removed++;
            }
        }

        _anchorScratch.Clear();
        return removed;
    }

    /// <summary>Synchronizes brick-entities for every matching brick within a pasted structure region.</summary>
    public void SyncRegion(
        DataStore store,
        Uuid structureUuid,
        VoxelObject voxelObject,
        Int3 origin,
        int sizeX,
        int sizeY,
        int sizeZ
    )
    {
        if (structureUuid == Uuid.Null || sizeX <= 0 || sizeY <= 0 || sizeZ <= 0)
        {
            return;
        }

        for (var z = 0; z < sizeZ; z++)
        {
            for (var y = 0; y < sizeY; y++)
            {
                for (var x = 0; x < sizeX; x++)
                {
                    var coord = new Int3(origin.X + x, origin.Y + y, origin.Z + z);
                    Voxel voxel = voxelObject.Get(coord.X, coord.Y, coord.Z);
                    var anchor = new BrickAnchorComponent(structureUuid, coord);

                    if (voxel.ID == 0)
                    {
                        RemoveAt(store, anchor);
                        continue;
                    }

                    Result<Brick> lookup = _brickDatabase.Get(voxel.ID);
                    if (!lookup.Success || !IsServerBrickEntity(lookup.Value))
                    {
                        RemoveAt(store, anchor);
                        continue;
                    }

                    Brick brick = lookup.Value;
                    if (_byAnchor.TryGetValue(anchor, out int existing)
                        && _brickByEntity.TryGetValue(existing, out Brick? existingBrick)
                        && string.Equals(existingBrick.ID, brick.ID, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    SpawnBrickEntity(store, structureUuid, coord, brick, persisted: null);
                }
            }
        }
    }

    /// <summary>Scans a freshly created or spawned structure and allocates brick-entities for all matching bricks.</summary>
    public int SyncStructure(DataStore store, Uuid structureUuid, VoxelObject voxelObject)
    {
        return RestoreStructure(store, structureUuid, voxelObject, persistedByKey: null, matchedKeys: null);
    }

    /// <summary>
    /// Scans <paramref name="voxelObject"/> and allocates brick-entities for every matching brick,
    /// restoring matching persisted records from <paramref name="persistedByKey"/> and recording matched keys.
    /// </summary>
    public int RestoreStructure(
        DataStore store,
        Uuid structureUuid,
        VoxelObject voxelObject,
        IReadOnlyDictionary<BrickEntityKey, BrickEntityData>? persistedByKey,
        HashSet<BrickEntityKey>? matchedKeys
    )
    {
        if (structureUuid == Uuid.Null || voxelObject == null)
        {
            return 0;
        }

        ulong rawStructureUuid = structureUuid.ToValue();
        byte chunkSize = voxelObject.ChunkSize;
        int shift = BitOperations.TrailingZeroCount(chunkSize);
        int shift2 = shift * 2;
        int mask = chunkSize - 1;
        var spawnedCount = 0;

        foreach (ChunkData chunk in voxelObject)
        {
            if (chunk.Palette.Only(0))
            {
                continue;
            }

            int baseX = chunk.Coords.X * chunkSize;
            int baseY = chunk.Coords.Y * chunkSize;
            int baseZ = chunk.Coords.Z * chunkSize;
            Voxel[] voxels = chunk.Data.Voxels;

            for (var i = 0; i < voxels.Length; i++)
            {
                ushort brickDataId = voxels[i].ID;
                if (brickDataId == 0)
                {
                    continue;
                }

                Result<Brick> lookup = _brickDatabase.Get(brickDataId);
                if (!lookup.Success || !IsServerBrickEntity(lookup.Value))
                {
                    continue;
                }

                Brick brick = lookup.Value;
                var coord = new Int3(
                    baseX + (i & mask),
                    baseY + ((i >> shift) & mask),
                    baseZ + (i >> shift2));

                BrickEntityData? persisted = null;
                if (persistedByKey != null)
                {
                    var key = new BrickEntityKey(rawStructureUuid, coord.X, coord.Y, coord.Z);
                    if (persistedByKey.TryGetValue(key, out BrickEntityData record)
                        && (string.IsNullOrEmpty(record.BrickId)
                            || string.Equals(record.BrickId, brick.ID, StringComparison.Ordinal)))
                    {
                        persisted = record;
                        matchedKeys?.Add(key);
                    }
                }

                SpawnBrickEntity(store, structureUuid, coord, brick, persisted);
                spawnedCount++;
            }
        }

        return spawnedCount;
    }

    /// <summary>Subscribes <paramref name="clientId"/> as a viewer of <paramref name="brickEntity"/> after reach and access validation.</summary>
    public Result AttachViewer(DataStore store, Uuid clientId, int brickEntity)
    {
        if (_replication == null)
        {
            return Result.FromFailure("Replication service is unavailable.");
        }

        if (clientId == Uuid.Null || brickEntity == Entity.Null)
        {
            return Result.FromFailure("Invalid viewer or brick-entity.");
        }

        if (!store.TryGet(brickEntity, out BrickAnchorComponent anchor)
            || !_byAnchor.TryGetValue(anchor, out int indexed)
            || indexed != brickEntity)
        {
            return Result.FromFailure("Target entity is not an active brick-entity.");
        }

        if (_sessions == null || !_sessions.TryGetEntity(clientId, out int playerEntity))
        {
            return Result.FromFailure("Viewer client has no active session.");
        }

        Result access = ValidateViewerAccess(store, clientId, playerEntity, brickEntity, in anchor);
        if (!access.Success)
        {
            return access;
        }

        _replication.SubscribeViewer(clientId, brickEntity);
        return Result.FromSuccess();
    }

    /// <summary>Detaches <paramref name="clientId"/> from <paramref name="brickEntity"/> and queues its client despawn.</summary>
    public bool DetachViewer(DataStore store, Uuid clientId, int brickEntity)
    {
        if (_replication == null || clientId == Uuid.Null || brickEntity == Entity.Null)
        {
            return false;
        }

        ulong entityUuid = store.GetUuid(brickEntity).ToValue();
        return _replication.UnsubscribeViewer(clientId, brickEntity, entityUuid);
    }

    /// <summary>Detaches any viewer whose brick-entity is no longer live, reachable, or permitted.</summary>
    public void DetachOutOfReachViewers(DataStore store)
    {
        if (_replication == null || _replication.ActiveViewerSubscriptionCount == 0)
        {
            return;
        }

        _replication.CopyActiveSubscriptions(_subscriptionScratch);
        for (var i = 0; i < _subscriptionScratch.Count; i++)
        {
            (Uuid clientId, int brickEntity) = _subscriptionScratch[i];
            if (!IsViewerStillAuthorized(store, clientId, brickEntity))
            {
                ulong entityUuid = store.GetUuid(brickEntity).ToValue();
                _replication.UnsubscribeViewer(clientId, brickEntity, entityUuid);
            }
        }

        _subscriptionScratch.Clear();
    }

    /// <summary>Programmatically seeds <paramref name="items"/> into <paramref name="groupId"/> on <paramref name="brickEntity"/>.</summary>
    public Result<int> SeedItems(DataStore store, int brickEntity, string groupId, ReadOnlySpan<ItemData> items)
    {
        if (brickEntity == Entity.Null || !store.TryGet(brickEntity, out InventoryComponent inventory))
        {
            return Result<int>.FromFailure("Target brick-entity does not carry an inventory.");
        }

        if (string.IsNullOrEmpty(groupId) || !inventory.HasGroup(groupId))
        {
            return Result<int>.FromFailure($"Brick-entity inventory does not declare slot group \"{groupId}\".");
        }

        InventoryComponent updated = inventory.Clone();
        var added = 0;
        for (var i = 0; i < items.Length; i++)
        {
            ItemData stack = ClampItemStack(items[i]);
            if (stack.IsEmpty())
            {
                continue;
            }

            if (updated.Add(in stack, targetGroupId: groupId))
            {
                added++;
            }
        }

        if (added > 0 || !HasSameSparseEntries(in inventory, in updated))
        {
            store.AddOrUpdate(brickEntity, updated);
        }

        return Result<int>.FromSuccess(added);
    }

    /// <summary>Programmatically seeds explicit keyed slot entries onto <paramref name="brickEntity"/>.</summary>
    public Result<int> SeedSlots(DataStore store, int brickEntity, ReadOnlySpan<KeyedSlotEntry> entries)
    {
        if (brickEntity == Entity.Null || !store.TryGet(brickEntity, out InventoryComponent inventory))
        {
            return Result<int>.FromFailure("Target brick-entity does not carry an inventory.");
        }

        InventoryComponent updated = inventory.Clone();
        var applied = 0;
        for (var i = 0; i < entries.Length; i++)
        {
            KeyedSlotEntry entry = entries[i];
            var slot = new SlotId(entry.GroupId, entry.LocalIndex);
            ItemData stack = ClampItemStack(entry.Item);
            if (!stack.IsEmpty() && updated.Set(in slot, stack))
            {
                applied++;
            }
        }

        if (applied > 0)
        {
            store.AddOrUpdate(brickEntity, updated);
        }

        return Result<int>.FromSuccess(applied);
    }

    /// <summary>Captures all live brick-entities from <paramref name="store"/> for <c>level.db</c> persistence.</summary>
    public BrickEntityData[] Capture(DataStore store)
    {
        if (_byAnchor.Count == 0)
        {
            return [];
        }

        var captured = new List<BrickEntityData>(_byAnchor.Count);
        foreach ((BrickAnchorComponent anchor, int entity) in _byAnchor)
        {
            if (store.GetUuid(entity) == Uuid.Null)
            {
                continue;
            }

            bool hasMachine = store.TryGet(entity, out MachineStateComponent machine);
            bool hasInventory = store.TryGet(entity, out InventoryComponent inventory);
            if (!hasMachine && !hasInventory)
            {
                continue;
            }

            string brickId = _brickByEntity.TryGetValue(entity, out Brick? recorded)
                ? recorded.ID
                : string.Empty;

            KeyedSlotEntry[]? inventoryEntries = hasInventory
                ? inventory.ToSparseEntries()
                : null;

            string stationTag = hasMachine ? machine.StationTag ?? string.Empty : string.Empty;
            string? activeRecipeId = hasMachine ? machine.ActiveRecipeId : null;
            byte persistentFlags = hasMachine
                ? (byte)(machine.StatusFlags & (byte)MachineStatusFlags.PersistentMask)
                : (byte)MachineStatusFlags.None;
            float progress = hasMachine && float.IsFinite(machine.Progress) && machine.Progress > 0f ? machine.Progress : 0f;
            float duration = hasMachine && float.IsFinite(machine.Duration) && machine.Duration > 0f ? machine.Duration : 0f;
            bool powerEnabled = hasMachine ? machine.PowerEnabled : true;

            captured.Add(new BrickEntityData(
                _VoxelEntityUuid: anchor.VoxelEntityUuid.ToValue(),
                _VoxelX: anchor.VoxelCoord.X,
                _VoxelY: anchor.VoxelCoord.Y,
                _VoxelZ: anchor.VoxelCoord.Z,
                _BrickId: brickId,
                _StationTag: stationTag,
                _ActiveRecipeId: activeRecipeId,
                _Progress: progress,
                _Duration: duration,
                _PowerEnabled: powerEnabled,
                _StatusFlags: persistentFlags,
                _Inventory: inventoryEntries));
        }

        return captured.Count > 0 ? [.. captured] : [];
    }

    /// <summary>Clears the lookup index and viewer subscriptions (used during level unload).</summary>
    public void Clear()
    {
        _byAnchor.Clear();
        _anchorsByStructure.Clear();
        _brickByEntity.Clear();
        _replication?.ClearAllViewers();
    }

    private int SpawnBrickEntity(
        DataStore store,
        Uuid structureUuid,
        Int3 coord,
        Brick brick,
        BrickEntityData? persisted
    )
    {
        var anchor = new BrickAnchorComponent(structureUuid, coord);

        if (_byAnchor.TryGetValue(anchor, out int existing))
        {
            DestroyBrickEntity(store, anchor, existing);
        }

        Uuid entityUuid = BrickEntityId.DeriveUnique(structureUuid, coord, store);
        int entity = store.Alloc(entityUuid);

        store.AddOrUpdate(entity, new NetworkComponent());
        store.AddOrUpdate(entity, anchor);

        for (var i = 0; i < _decorators.Length; i++)
        {
            if (_decorators[i].AppliesTo(brick))
            {
                _decorators[i].OnSpawn(store, entity, brick, in persisted);
            }
        }

        _byAnchor[anchor] = entity;
        _brickByEntity[entity] = brick;
        if (!_anchorsByStructure.TryGetValue(structureUuid, out HashSet<BrickAnchorComponent>? anchors))
        {
            anchors = [];
            _anchorsByStructure[structureUuid] = anchors;
        }

        anchors.Add(anchor);
        _logger?.LogDebug(
            "Allocated brick-entity {entity} ({uuid}) for brick \"{brick}\" at ({structure}, {coord}).",
            entity, entityUuid, brick.ID, structureUuid, coord);
        return entity;
    }

    private void RemoveAt(DataStore store, in BrickAnchorComponent anchor)
    {
        if (_byAnchor.TryGetValue(anchor, out int existing))
        {
            DestroyBrickEntity(store, anchor, existing);
        }
    }

    private void DestroyBrickEntity(DataStore store, in BrickAnchorComponent anchor, int brickEntity)
    {
        _byAnchor.Remove(anchor);
        bool hadBrick = _brickByEntity.Remove(brickEntity, out Brick? brick);

        if (_anchorsByStructure.TryGetValue(anchor.VoxelEntityUuid, out HashSet<BrickAnchorComponent>? anchors))
        {
            anchors.Remove(anchor);
            if (anchors.Count == 0)
            {
                _anchorsByStructure.Remove(anchor.VoxelEntityUuid);
            }
        }

        if (hadBrick && brick != null)
        {
            for (var i = 0; i < _decorators.Length; i++)
            {
                if (_decorators[i].AppliesTo(brick))
                {
                    _decorators[i].OnDespawn(store, brickEntity, brick);
                }
            }
        }

        Uuid entityUuid = store.GetUuid(brickEntity);
        ulong rawUuid = entityUuid.ToValue();
        _replication?.DetachAllViewers(brickEntity, rawUuid);
        BrickEntityDestroyed?.Invoke(store, brickEntity, entityUuid);

        if (rawUuid != 0UL)
        {
            store.Free(brickEntity);
        }
    }

    private void TransferContentsToPlayer(DataStore store, int breakingPlayer, in InventoryComponent brickInventory)
    {
        KeyedSlotEntry[] entries = brickInventory.ToSparseEntries();
        if (entries.Length == 0 || !store.TryGet(breakingPlayer, out InventoryComponent playerInventory))
        {
            return;
        }

        InventoryComponent updated = playerInventory.Clone();
        for (var i = 0; i < entries.Length; i++)
        {
            ItemData item = ClampItemStack(entries[i].Item);
            if (item.IsEmpty())
            {
                continue;
            }

            if (!updated.Add(in item))
            {
                _logger?.LogDebug(
                    "Player {player} inventory could not accept all {count}x \"{item}\" from broken brick-entity.",
                    breakingPlayer, item.Count, item.ID);
            }
        }

        if (!HasSameSparseEntries(in playerInventory, in updated))
        {
            store.AddOrUpdate(breakingPlayer, updated);
        }
    }

    private static bool HasSameSparseEntries(in InventoryComponent left, in InventoryComponent right)
    {
        KeyedSlotEntry[] a = left.ToSparseEntries();
        KeyedSlotEntry[] b = right.ToSparseEntries();
        if (a.Length != b.Length)
        {
            return false;
        }

        for (var i = 0; i < a.Length; i++)
        {
            if (!string.Equals(a[i].GroupId, b[i].GroupId, StringComparison.Ordinal)
                || a[i].LocalIndex != b[i].LocalIndex
                || !string.Equals(a[i].Item.ID, b[i].Item.ID, StringComparison.Ordinal)
                || a[i].Item.Count != b[i].Item.Count)
            {
                return false;
            }
        }

        return true;
    }

    internal Result ValidateViewerAccess(
        DataStore store,
        Uuid clientId,
        int playerEntity,
        int brickEntity,
        in BrickAnchorComponent anchor
    )
    {
        Uuid playerUuid = store.GetUuid(playerEntity);
        if (playerUuid == Uuid.Null
            || !store.TryGet(playerEntity, out TransformComponent playerTransform)
            || !IsFinite(playerTransform.Position))
        {
            return Result.FromFailure("Viewer transform is unavailable.");
        }

        if (!TryResolveWorldPosition(store, in anchor, out Vector3 worldPosition))
        {
            return Result.FromFailure("Brick-entity parent structure is unavailable.");
        }

        float maxReach = CharacterDefaults.DEFAULT_REACH;
        if (store.TryGet(brickEntity, out ContainerAccessComponent access))
        {
            if (access.MaxReach > 0f && float.IsFinite(access.MaxReach))
            {
                maxReach = access.MaxReach;
            }

            if (access.ActiveViewer != Uuid.Null && access.ActiveViewer != playerUuid)
            {
                return Result.FromFailure("Brick-entity is currently viewed exclusively by another player.");
            }

            if (access.IsLocked && access.Owner != playerUuid)
            {
                return Result.FromFailure("Brick-entity is locked.");
            }

            if (!string.IsNullOrEmpty(access.RequiredPermission)
                && (_permissions == null || !_permissions.HasPermission(clientId, access.RequiredPermission)))
            {
                return Result.FromFailure("Viewer lacks required brick-entity permission.");
            }
        }

        if (Vector3.DistanceSquared(playerTransform.Position, worldPosition) > maxReach * maxReach)
        {
            return Result.FromFailure("Brick-entity is out of reach.");
        }

        return Result.FromSuccess();
    }

    private bool IsViewerStillAuthorized(DataStore store, Uuid clientId, int brickEntity)
    {
        if (_sessions == null
            || !_sessions.TryGetEntity(clientId, out int playerEntity)
            || !store.TryGet(brickEntity, out BrickAnchorComponent anchor)
            || !_byAnchor.TryGetValue(anchor, out int indexed)
            || indexed != brickEntity)
        {
            return false;
        }

        return ValidateViewerAccess(store, clientId, playerEntity, brickEntity, in anchor).Success;
    }

    private ItemData ClampItemStack(in ItemData item)
    {
        if (item.IsEmpty())
        {
            return default;
        }

        int maxSize = item.MaxSize > 0 ? item.MaxSize : 100;
        if (_itemDatabase != null)
        {
            Result<Item> resolved = _itemDatabase.Get(item.ID);
            if (resolved.Success && resolved.Value.MaxStack > 0)
            {
                maxSize = resolved.Value.MaxStack;
            }
        }

        int count = Math.Clamp(item.Count, 1, maxSize);
        return new ItemData(item.ID, count, maxSize);
    }

    private static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    }

    private static bool IsFinite(Quaternion value)
    {
        return float.IsFinite(value.X)
            && float.IsFinite(value.Y)
            && float.IsFinite(value.Z)
            && float.IsFinite(value.W);
    }
}
