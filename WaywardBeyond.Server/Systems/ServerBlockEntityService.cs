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
/// Per-world authoritative manager for server ECS block-entities anchored to functional bricks.
/// Maintains the <c>(VoxelEntityUuid, Int3) -> Entity</c> lookup index, handles placement, break,
/// region/blueprint sync, viewer attachment and reach eviction, programmatic cargo seeding, and
/// <c>level.db</c> capture/restore.
/// </summary>
public sealed class ServerBlockEntityService(
    IBrickDatabase brickDatabase,
    NetworkReplicationSystem? replication = null,
    SessionManager? sessions = null,
    ISlotGroupSchemaDatabase? schemas = null,
    IItemDatabase? itemDatabase = null,
    IUserPermissionService? permissions = null,
    ILogger<ServerBlockEntityService>? logger = null
)
{
    private const ulong BLOCK_ENTITY_UUID_SALT = 0x517C_C1B7_2722_0A95UL;
    private const ulong SPLITMIX_INCREMENT = 0x9E37_79B9_7F4A_7C15UL;

    private readonly Dictionary<BrickAnchorComponent, int> _byAnchor = [];
    private readonly Dictionary<Uuid, HashSet<BrickAnchorComponent>> _anchorsByStructure = [];
    private readonly Dictionary<int, string> _brickIdByEntity = [];
    private readonly List<(Uuid ClientId, int Entity)> _subscriptionScratch = [];
    private readonly List<BrickAnchorComponent> _anchorScratch = [];

    /// <summary>The number of live server block-entities tracked by this world.</summary>
    public int Count => _byAnchor.Count;

    /// <summary>Attempts to resolve the server ECS block-entity anchored at <paramref name="anchor"/>.</summary>
    public bool TryGetEntity(in BrickAnchorComponent anchor, out int entity)
    {
        return _byAnchor.TryGetValue(anchor, out entity);
    }

    /// <summary>Attempts to resolve the server ECS block-entity anchored at (<paramref name="structureUuid"/>, <paramref name="coord"/>).</summary>
    public bool TryGetEntity(Uuid structureUuid, Int3 coord, out int entity)
    {
        return _byAnchor.TryGetValue(new BrickAnchorComponent(structureUuid, coord), out entity);
    }

    /// <summary>Gets all live block-entity ids anchored to <paramref name="structureUuid"/>.</summary>
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

    /// <summary>Attempts to find the first block-entity on <paramref name="structureUuid"/> matching <paramref name="stationTag"/>.</summary>
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

    /// <summary>Computes the live world-space position of the block-entity anchored by <paramref name="anchor"/>.</summary>
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
    /// Allocates or updates the server block-entity when a brick is placed at (<paramref name="structureUuid"/>, <paramref name="coord"/>).
    /// Returns the allocated entity id, or <see cref="Entity.Null"/> when the brick has no block-entity metadata.
    /// </summary>
    public int OnBrickPlaced(DataStore store, Uuid structureUuid, Int3 coord, ushort brickDataId)
    {
        if (structureUuid == Uuid.Null || brickDataId == 0)
        {
            return Entity.Null;
        }

        Result<Brick> lookup = brickDatabase.Get(brickDataId);
        if (!lookup.Success || !lookup.Value.HasBlockEntity)
        {
            RemoveAt(store, new BrickAnchorComponent(structureUuid, coord));
            return Entity.Null;
        }

        return SpawnBlockEntity(store, structureUuid, coord, lookup.Value, persisted: null);
    }

    /// <summary>
    /// Handles breaking a functional brick: transfers stored contents to <paramref name="breakingPlayer"/>
    /// in non-Creative modes, tears down active viewer sessions, and frees the block-entity.
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
        if (!_byAnchor.TryGetValue(anchor, out int blockEntity))
        {
            return false;
        }

        if (gameMode != GameMode.Creative
            && breakingPlayer != Entity.Null
            && store.TryGet(blockEntity, out InventoryComponent blockInventory))
        {
            TransferContentsToPlayer(store, breakingPlayer, in blockInventory);
        }

        DestroyBlockEntity(store, anchor, blockEntity);
        return true;
    }

    /// <summary>Removes and frees every block-entity anchored to <paramref name="structureUuid"/>.</summary>
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
            if (_byAnchor.TryGetValue(anchor, out int blockEntity))
            {
                DestroyBlockEntity(store, anchor, blockEntity);
                removed++;
            }
        }

        _anchorScratch.Clear();
        _anchorsByStructure.Remove(structureUuid);
        return removed;
    }

    /// <summary>Removes and frees every block-entity on <paramref name="structureUuid"/> inside [<paramref name="min"/>, <paramref name="max"/>].</summary>
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
            if (_byAnchor.TryGetValue(anchor, out int blockEntity))
            {
                DestroyBlockEntity(store, anchor, blockEntity);
                removed++;
            }
        }

        _anchorScratch.Clear();
        return removed;
    }

    /// <summary>Synchronizes block-entities for every functional brick within a pasted structure region.</summary>
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

                    Result<Brick> lookup = brickDatabase.Get(voxel.ID);
                    if (!lookup.Success || !lookup.Value.HasBlockEntity)
                    {
                        RemoveAt(store, anchor);
                        continue;
                    }

                    Brick brick = lookup.Value;
                    if (_byAnchor.TryGetValue(anchor, out int existing)
                        && _brickIdByEntity.TryGetValue(existing, out string? existingBrickId)
                        && string.Equals(existingBrickId, brick.ID, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    SpawnBlockEntity(store, structureUuid, coord, brick, persisted: null);
                }
            }
        }
    }

    /// <summary>Scans a freshly created or spawned structure and allocates block-entities for all functional bricks.</summary>
    public int SyncStructure(DataStore store, Uuid structureUuid, VoxelObject voxelObject)
    {
        return RestoreStructure(store, structureUuid, voxelObject, persistedByKey: null, matchedKeys: null);
    }

    /// <summary>
    /// Scans <paramref name="voxelObject"/> and allocates block-entities for every functional brick,
    /// restoring matching persisted records from <paramref name="persistedByKey"/> and recording matched keys.
    /// </summary>
    public int RestoreStructure(
        DataStore store,
        Uuid structureUuid,
        VoxelObject voxelObject,
        IReadOnlyDictionary<BlockEntityKey, BlockEntityData>? persistedByKey,
        HashSet<BlockEntityKey>? matchedKeys
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

                Result<Brick> lookup = brickDatabase.Get(brickDataId);
                if (!lookup.Success || !lookup.Value.HasBlockEntity)
                {
                    continue;
                }

                Brick brick = lookup.Value;
                var coord = new Int3(
                    baseX + (i & mask),
                    baseY + ((i >> shift) & mask),
                    baseZ + (i >> shift2));

                BlockEntityData? persisted = null;
                if (persistedByKey != null)
                {
                    var key = new BlockEntityKey(rawStructureUuid, coord.X, coord.Y, coord.Z);
                    if (persistedByKey.TryGetValue(key, out BlockEntityData record)
                        && (string.IsNullOrEmpty(record.BrickId)
                            || string.Equals(record.BrickId, brick.ID, StringComparison.Ordinal)))
                    {
                        persisted = record;
                        matchedKeys?.Add(key);
                    }
                }

                SpawnBlockEntity(store, structureUuid, coord, brick, persisted);
                spawnedCount++;
            }
        }

        return spawnedCount;
    }

    /// <summary>Subscribes <paramref name="clientId"/> as a viewer of <paramref name="blockEntity"/> after reach and access validation.</summary>
    public Result AttachViewer(DataStore store, Uuid clientId, int blockEntity)
    {
        if (replication == null)
        {
            return Result.FromFailure("Replication service is unavailable.");
        }

        if (clientId == Uuid.Null || blockEntity == Entity.Null)
        {
            return Result.FromFailure("Invalid viewer or block-entity.");
        }

        if (!store.TryGet(blockEntity, out BrickAnchorComponent anchor)
            || !_byAnchor.TryGetValue(anchor, out int indexed)
            || indexed != blockEntity)
        {
            return Result.FromFailure("Target entity is not an active block-entity.");
        }

        if (sessions == null || !sessions.TryGetEntity(clientId, out int playerEntity))
        {
            return Result.FromFailure("Viewer client has no active session.");
        }

        Result access = ValidateViewerAccess(store, clientId, playerEntity, blockEntity, in anchor);
        if (!access.Success)
        {
            return access;
        }

        replication.SubscribeViewer(clientId, blockEntity);
        return Result.FromSuccess();
    }

    /// <summary>Detaches <paramref name="clientId"/> from <paramref name="blockEntity"/> and queues its client despawn.</summary>
    public bool DetachViewer(DataStore store, Uuid clientId, int blockEntity)
    {
        if (replication == null || clientId == Uuid.Null || blockEntity == Entity.Null)
        {
            return false;
        }

        ulong entityUuid = store.GetUuid(blockEntity).ToValue();
        return replication.UnsubscribeViewer(clientId, blockEntity, entityUuid);
    }

    /// <summary>Detaches any viewer whose block-entity is no longer live, reachable, or permitted.</summary>
    public void DetachOutOfReachViewers(DataStore store)
    {
        if (replication == null || replication.ActiveViewerSubscriptionCount == 0)
        {
            return;
        }

        replication.CopyActiveSubscriptions(_subscriptionScratch);
        for (var i = 0; i < _subscriptionScratch.Count; i++)
        {
            (Uuid clientId, int blockEntity) = _subscriptionScratch[i];
            if (!IsViewerStillAuthorized(store, clientId, blockEntity))
            {
                ulong entityUuid = store.GetUuid(blockEntity).ToValue();
                replication.UnsubscribeViewer(clientId, blockEntity, entityUuid);
            }
        }

        _subscriptionScratch.Clear();
    }

    /// <summary>Programmatically seeds <paramref name="items"/> into <paramref name="groupId"/> on <paramref name="blockEntity"/>.</summary>
    public Result<int> SeedItems(DataStore store, int blockEntity, string groupId, ReadOnlySpan<ItemData> items)
    {
        if (blockEntity == Entity.Null || !store.TryGet(blockEntity, out InventoryComponent inventory))
        {
            return Result<int>.FromFailure("Target block-entity does not carry an inventory.");
        }

        if (string.IsNullOrEmpty(groupId) || !inventory.HasGroup(groupId))
        {
            return Result<int>.FromFailure($"Block-entity inventory does not declare slot group \"{groupId}\".");
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
            store.AddOrUpdate(blockEntity, updated);
        }

        return Result<int>.FromSuccess(added);
    }

    /// <summary>Programmatically seeds explicit keyed slot entries onto <paramref name="blockEntity"/>.</summary>
    public Result<int> SeedSlots(DataStore store, int blockEntity, ReadOnlySpan<KeyedSlotEntry> entries)
    {
        if (blockEntity == Entity.Null || !store.TryGet(blockEntity, out InventoryComponent inventory))
        {
            return Result<int>.FromFailure("Target block-entity does not carry an inventory.");
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
            store.AddOrUpdate(blockEntity, updated);
        }

        return Result<int>.FromSuccess(applied);
    }

    /// <summary>Captures all live block-entities from <paramref name="store"/> for <c>level.db</c> persistence.</summary>
    public BlockEntityData[] Capture(DataStore store)
    {
        if (_byAnchor.Count == 0)
        {
            return [];
        }

        var captured = new List<BlockEntityData>(_byAnchor.Count);
        foreach ((BrickAnchorComponent anchor, int entity) in _byAnchor)
        {
            if (store.GetUuid(entity) == Uuid.Null)
            {
                continue;
            }

            if (!store.TryGet(entity, out MachineStateComponent machine))
            {
                continue;
            }

            string brickId = _brickIdByEntity.TryGetValue(entity, out string? recorded)
                ? recorded
                : string.Empty;

            KeyedSlotEntry[]? inventoryEntries = store.TryGet(entity, out InventoryComponent inventory)
                ? inventory.ToSparseEntries()
                : null;

            byte persistentFlags = (byte)(machine.StatusFlags & (byte)MachineStatusFlags.PersistentMask);
            float progress = float.IsFinite(machine.Progress) && machine.Progress > 0f ? machine.Progress : 0f;
            float duration = float.IsFinite(machine.Duration) && machine.Duration > 0f ? machine.Duration : 0f;

            captured.Add(new BlockEntityData(
                _VoxelEntityUuid: anchor.VoxelEntityUuid.ToValue(),
                _VoxelX: anchor.VoxelCoord.X,
                _VoxelY: anchor.VoxelCoord.Y,
                _VoxelZ: anchor.VoxelCoord.Z,
                _BrickId: brickId,
                _StationTag: machine.StationTag ?? string.Empty,
                _ActiveRecipeId: machine.ActiveRecipeId,
                _Progress: progress,
                _Duration: duration,
                _PowerEnabled: machine.PowerEnabled,
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
        _brickIdByEntity.Clear();
        replication?.ClearAllViewers();
    }

    /// <summary>Derives a deterministic, non-zero, collision-free entity UUID for (<paramref name="structureUuid"/>, <paramref name="coord"/>).</summary>
    internal static Uuid DeriveUuid(Uuid structureUuid, Int3 coord, DataStore store)
    {
        ulong hash = MixBlockEntityHash(structureUuid.ToValue(), coord.X, coord.Y, coord.Z);
        Uuid candidate = Uuid.FromValue(hash);
        while (candidate == Uuid.Null || store.TryGet(candidate, out _))
        {
            hash = SplitMix64(hash + SPLITMIX_INCREMENT);
            candidate = Uuid.FromValue(hash);
        }

        return candidate;
    }

    private int SpawnBlockEntity(
        DataStore store,
        Uuid structureUuid,
        Int3 coord,
        Brick brick,
        BlockEntityData? persisted
    )
    {
        BrickBlockEntity metadata = brick.BlockEntity!;
        var anchor = new BrickAnchorComponent(structureUuid, coord);

        if (_byAnchor.TryGetValue(anchor, out int existing))
        {
            DestroyBlockEntity(store, anchor, existing);
        }

        Uuid entityUuid = DeriveUuid(structureUuid, coord, store);
        int entity = store.Alloc(entityUuid);

        store.AddOrUpdate(entity, new NetworkComponent());
        store.AddOrUpdate(entity, anchor);
        store.AddOrUpdate(entity, new ContainerAccessComponent
        {
            MaxReach = CharacterDefaults.DEFAULT_REACH,
        });

        if (metadata.HasInventory)
        {
            InventoryComponent inventory = BuildInventory(metadata.SlotGroups, persisted?.Inventory);
            store.AddOrUpdate(entity, inventory);
        }

        float progress = persisted != null && float.IsFinite(persisted.Value.Progress) && persisted.Value.Progress > 0f
            ? persisted.Value.Progress
            : 0f;
        float duration = persisted != null && float.IsFinite(persisted.Value.Duration) && persisted.Value.Duration > 0f
            ? persisted.Value.Duration
            : 0f;
        bool powerEnabled = persisted?.PowerEnabled ?? metadata.DefaultPowerEnabled;
        byte statusFlags = persisted != null
            ? (byte)(persisted.Value.StatusFlags & (byte)MachineStatusFlags.PersistentMask)
            : (metadata.DefaultOpen ? (byte)MachineStatusFlags.Open : (byte)MachineStatusFlags.None);

        var machineState = new MachineStateComponent(
            _StationTag: metadata.StationTag,
            _ActiveRecipeId: persisted?.ActiveRecipeId,
            _Progress: progress,
            _Duration: duration,
            _PowerEnabled: powerEnabled,
            _HasPower: false,
            _StatusFlags: statusFlags);
        store.AddOrUpdate(entity, machineState);

        _byAnchor[anchor] = entity;
        _brickIdByEntity[entity] = brick.ID;
        if (!_anchorsByStructure.TryGetValue(structureUuid, out HashSet<BrickAnchorComponent>? anchors))
        {
            anchors = [];
            _anchorsByStructure[structureUuid] = anchors;
        }

        anchors.Add(anchor);
        logger?.LogDebug(
            "Allocated block-entity {entity} ({uuid}) for brick \"{brick}\" at ({structure}, {coord}).",
            entity, entityUuid, brick.ID, structureUuid, coord);
        return entity;
    }

    private InventoryComponent BuildInventory(IReadOnlyList<BrickSlotGroup> slotGroups, KeyedSlotEntry[]? persistedEntries)
    {
        ISlotGroupSchemaDatabase resolvedSchemas = schemas ?? SlotGroupSchemaDatabase.Default;
        var descriptors = new SlotGroupDescriptor[slotGroups.Count];
        for (var i = 0; i < slotGroups.Count; i++)
        {
            BrickSlotGroup group = slotGroups[i];
            int capacity = group.Capacity ?? resolvedSchemas.GetCapacity(group.GroupId, GetDefaultGroupCapacity(group.GroupId));
            descriptors[i] = new SlotGroupDescriptor(group.GroupId, Math.Max(0, capacity));
        }

        var inventory = InventoryComponent.Create(descriptors);
        if (persistedEntries != null && persistedEntries.Length > 0)
        {
            var sanitized = new KeyedSlotEntry[persistedEntries.Length];
            for (var i = 0; i < persistedEntries.Length; i++)
            {
                KeyedSlotEntry entry = persistedEntries[i];
                sanitized[i] = new KeyedSlotEntry(entry.GroupId, entry.LocalIndex, ClampItemStack(entry.Item));
            }

            inventory.LoadSparse(sanitized);
        }

        return inventory;
    }

    private void RemoveAt(DataStore store, in BrickAnchorComponent anchor)
    {
        if (_byAnchor.TryGetValue(anchor, out int existing))
        {
            DestroyBlockEntity(store, anchor, existing);
        }
    }

    private void DestroyBlockEntity(DataStore store, in BrickAnchorComponent anchor, int blockEntity)
    {
        _byAnchor.Remove(anchor);
        _brickIdByEntity.Remove(blockEntity);

        if (_anchorsByStructure.TryGetValue(anchor.VoxelEntityUuid, out HashSet<BrickAnchorComponent>? anchors))
        {
            anchors.Remove(anchor);
            if (anchors.Count == 0)
            {
                _anchorsByStructure.Remove(anchor.VoxelEntityUuid);
            }
        }

        ulong entityUuid = store.GetUuid(blockEntity).ToValue();
        replication?.DetachAllViewers(blockEntity, entityUuid);

        if (entityUuid != 0UL)
        {
            store.Free(blockEntity);
        }
    }

    private void TransferContentsToPlayer(DataStore store, int breakingPlayer, in InventoryComponent blockInventory)
    {
        KeyedSlotEntry[] entries = blockInventory.ToSparseEntries();
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
                logger?.LogDebug(
                    "Player {player} inventory could not accept all {count}x \"{item}\" from broken block-entity.",
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

    private Result ValidateViewerAccess(
        DataStore store,
        Uuid clientId,
        int playerEntity,
        int blockEntity,
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
            return Result.FromFailure("Block-entity parent structure is unavailable.");
        }

        float maxReach = CharacterDefaults.DEFAULT_REACH;
        if (store.TryGet(blockEntity, out ContainerAccessComponent access))
        {
            if (access.MaxReach > 0f && float.IsFinite(access.MaxReach))
            {
                maxReach = access.MaxReach;
            }

            if (access.ActiveViewer != Uuid.Null && access.ActiveViewer != playerUuid)
            {
                return Result.FromFailure("Block-entity is currently viewed exclusively by another player.");
            }

            if (access.IsLocked && access.Owner != playerUuid)
            {
                return Result.FromFailure("Block-entity is locked.");
            }

            if (!string.IsNullOrEmpty(access.RequiredPermission)
                && (permissions == null || !permissions.HasPermission(clientId, access.RequiredPermission)))
            {
                return Result.FromFailure("Viewer lacks required block-entity permission.");
            }
        }

        if (Vector3.DistanceSquared(playerTransform.Position, worldPosition) > maxReach * maxReach)
        {
            return Result.FromFailure("Block-entity is out of reach.");
        }

        return Result.FromSuccess();
    }

    private bool IsViewerStillAuthorized(DataStore store, Uuid clientId, int blockEntity)
    {
        if (sessions == null
            || !sessions.TryGetEntity(clientId, out int playerEntity)
            || !store.TryGet(blockEntity, out BrickAnchorComponent anchor)
            || !_byAnchor.TryGetValue(anchor, out int indexed)
            || indexed != blockEntity)
        {
            return false;
        }

        return ValidateViewerAccess(store, clientId, playerEntity, blockEntity, in anchor).Success;
    }

    private ItemData ClampItemStack(in ItemData item)
    {
        if (item.IsEmpty())
        {
            return default;
        }

        int maxSize = item.MaxSize > 0 ? item.MaxSize : 100;
        if (itemDatabase != null)
        {
            Result<Item> resolved = itemDatabase.Get(item.ID);
            if (resolved.Success && resolved.Value.MaxStack > 0)
            {
                maxSize = resolved.Value.MaxStack;
            }
        }

        int count = Math.Clamp(item.Count, 1, maxSize);
        return new ItemData(item.ID, count, maxSize);
    }

    private static int GetDefaultGroupCapacity(string groupId)
    {
        return groupId switch
        {
            SlotGroups.CARGO => SlotGroups.DEFAULT_CARGO_CAPACITY,
            SlotGroups.HOTBAR => SlotGroups.DEFAULT_HOTBAR_CAPACITY,
            SlotGroups.BACKPACK => SlotGroups.DEFAULT_BACKPACK_CAPACITY,
            SlotGroups.WEARABLES => SlotGroups.DEFAULT_WEARABLES_CAPACITY,
            SlotGroups.AUGMENTS => SlotGroups.DEFAULT_AUGMENTS_CAPACITY,
            _ => 1,
        };
    }

    private static ulong MixBlockEntityHash(ulong structureUuid, int x, int y, int z)
    {
        ulong h = BLOCK_ENTITY_UUID_SALT ^ SplitMix64(structureUuid);
        h = SplitMix64(h ^ (uint)x);
        h = SplitMix64(h ^ ((ulong)(uint)y << 32));
        h = SplitMix64(h ^ (uint)z);
        return h;
    }

    private static ulong SplitMix64(ulong value)
    {
        ulong z = value + SPLITMIX_INCREMENT;
        z = (z ^ (z >> 30)) * 0xBF58_476D_1CE4_E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D0_49BB_1331_11EBUL;
        return z ^ (z >> 31);
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
