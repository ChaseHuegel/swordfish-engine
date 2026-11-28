using System;
using System.Collections.Generic;
using System.Numerics;
using Krill;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Blueprints;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Statistics;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Gameplay.Networking;
using WaywardBeyond.Items;
using WaywardBeyond.Server.Components;
using WaywardBeyond.Server.Saves;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// Per-world authoritative Adventure Mode death and respawn service. Transitions zero-health Adventure
/// players into the dead state, halts motion and active viewers, clears timed buffs, atomically moves
/// hotbar and backpack items into a bundled 1-brick Death-Container structure while preserving equipped
/// wearables and augments, and executes server-authoritative respawn requests.
/// </summary>
public sealed class ServerDeathService(
    SimulationStep simulationStep,
    GameplayConfig gameplayConfig,
    NetworkReplicationSystem? replication = null,
    SessionManager? sessions = null,
    ServerPeers? peers = null,
    LevelSaveService? saveService = null,
    ServerBrickEntityService? brickEntities = null,
    IServerWorldPhysics? physics = null,
    IBrickRegistry? brickRegistry = null,
    IBrickDatabase? brickDatabase = null,
    IPaintRegistry? paintRegistry = null,
    IBundledBlueprintDatabase? bundledBlueprints = null,
    IItemDatabase? itemDatabase = null,
    ILogger<ServerDeathService>? logger = null
)
{
    private const string STORAGE_BRICK_ID = "wb:storage";

    private static readonly float[] _nudgeRadii = [1.0f, 2.0f, 3.0f, 4.5f];

    private static readonly Vector3[] _nudgeDirections = BuildNudgeDirections();

    private readonly List<ItemData> _droppedItemsScratch = [];

    private float DefaultHealthMax => SanitizeMax(gameplayConfig.Survival.MaxHealth.Get(), StatisticIds.DEFAULT_HEALTH_MAX);

    private float DefaultOxygenMax => SanitizeMax(gameplayConfig.Survival.MaxOxygen.Get(), StatisticIds.DEFAULT_OXYGEN_MAX);

    /// <summary>
    /// Transitions <paramref name="playerEntity"/> to the authoritative dead state when it is an alive
    /// Adventure Mode player at or below zero health. Returns whether a death transition occurred.
    /// </summary>
    public bool HandleDeathIfNeeded(DataStore store, int playerEntity, long simTick)
    {
        if (playerEntity == Entity.Null
            || !store.TryGet(playerEntity, out StatisticsComponent stats)
            || stats.IsDead)
        {
            return false;
        }

        if (store.TryGet(playerEntity, out GameModeComponent mode) && mode.Mode == GameMode.Creative)
        {
            return false;
        }

        float healthMax = SanitizeMax(stats.GetFixed(StatisticIds.HEALTH_MAX, DefaultHealthMax), DefaultHealthMax);
        float health = stats.GetFixed(StatisticIds.HEALTH, healthMax);
        if (!float.IsFinite(health) || health > 0f)
        {
            return false;
        }

        DamageSource cause = DamageSource.None;
        if (store.TryGet(playerEntity, out DamageStateComponent damageState))
        {
            cause = damageState.DeathCause != DamageSource.None
                ? damageState.DeathCause
                : damageState.LastSource;
        }

        Vector3 deathPosition = store.TryGet(playerEntity, out TransformComponent transform) && IsFinite(transform.Position)
            ? transform.Position
            : ResolveSpawnPosition();

        FreezePlayerMotion(store, playerEntity);
        DetachPlayerViewers(store, playerEntity);

        Vector3 recordedPosition = deathPosition;
        var containerSpawned = false;

        if (store.TryGet(playerEntity, out InventoryComponent playerInventory))
        {
            CollectDroppableItems(in playerInventory, _droppedItemsScratch);
            if (_droppedItemsScratch.Count > 0)
            {
                if (TrySpawnDeathContainer(
                    store,
                    playerEntity,
                    deathPosition,
                    _droppedItemsScratch,
                    out Vector3 containerPosition,
                    out int spawnedStructureEntity))
                {
                    InventoryComponent stripped = StripDroppedGroups(in playerInventory);
                    store.AddOrUpdate(playerEntity, stripped);

                    if (sessions != null && peers != null && brickRegistry != null)
                    {
                        VoxelEntityData rawSpawn = VoxelWorldEntityFactory.ToVoxelEntityData(store, spawnedStructureEntity);
                        Result<VoxelEntityData> encoded = VoxelCodec.Encode(in rawSpawn, brickRegistry, paintRegistry);
                        if (encoded.Success)
                        {
                            ServerVoxelEntityBroadcaster.BroadcastEntitySpawn(store, sessions, peers, logger, in encoded.Value);
                        }
                        else
                        {
                            logger?.LogWarning("Failed to encode Death-Container {uuid} for broadcast: {message}", store.GetUuid(spawnedStructureEntity), encoded.Message);
                        }
                    }

                    saveService?.QueueSaveStructure(store, spawnedStructureEntity);
                    recordedPosition = containerPosition;
                    containerSpawned = true;
                }
                else
                {
                    logger?.LogWarning(
                        "Failed to spawn Death-Container for player {player}; preserving player inventory to prevent item loss.",
                        playerEntity);
                }
            }

            _droppedItemsScratch.Clear();
        }

        ApplyDeadStatistics(store, playerEntity, cause, recordedPosition, containerSpawned);
        saveService?.QueueSaveCharacterLocation(store, playerEntity, (uint)Math.Max(0L, simTick));
        return true;
    }

    /// <summary>
    /// Respawns <paramref name="playerEntity"/> at the level spawn point when it is currently dead,
    /// resetting velocity, orientation, vitals, and death flags. Rejects requests for alive players.
    /// </summary>
    public Result RespawnPlayer(DataStore store, int playerEntity)
    {
        if (playerEntity == Entity.Null || !store.TryGet(playerEntity, out StatisticsComponent stats))
        {
            return Result.FromFailure("Player entity is unavailable.");
        }

        if (!stats.IsDead)
        {
            return Result.FromFailure("Player is not dead.");
        }

        Vector3 spawnPosition = ResolveSpawnPosition();
        Vector3 scale = CharacterDefaults.PlayerScale;
        if (store.TryGet(playerEntity, out TransformComponent existingTransform)
            && IsFinite(existingTransform.Scale)
            && existingTransform.Scale.X > 0f
            && existingTransform.Scale.Y > 0f
            && existingTransform.Scale.Z > 0f)
        {
            scale = existingTransform.Scale;
        }

        store.AddOrUpdate(playerEntity, new TransformComponent(spawnPosition, Quaternion.Identity, scale));
        FreezePlayerMotion(store, playerEntity);

        Uuid playerUuid = store.GetUuid(playerEntity);
        if (playerUuid != Uuid.Null)
        {
            simulationStep.ForgetLookHistory(playerUuid);
        }

        ApplyRespawnStatistics(store, playerEntity, in stats);

        if (store.TryGet(playerEntity, out DamageStateComponent damageState))
        {
            damageState.DeathCause = DamageSource.None;
            damageState.DeathTick = 0L;
            store.AddOrUpdate(playerEntity, damageState);
        }

        saveService?.QueueSaveCharacterLocation(store, playerEntity, simulationStep.CurrentSimTick);
        return Result.FromSuccess();
    }

    /// <summary>Returns a copy of <paramref name="inventory"/> with hotbar, backpack, and non-equipment orphans cleared.</summary>
    internal static InventoryComponent StripDroppedGroups(in InventoryComponent inventory)
    {
        InventoryComponent stripped = inventory.Clone();
        KeyedSlotEntry[] entries = stripped.ToSparseEntries();
        if (entries.Length == 0)
        {
            return stripped;
        }

        var kept = new List<KeyedSlotEntry>(entries.Length);
        for (var i = 0; i < entries.Length; i++)
        {
            KeyedSlotEntry entry = entries[i];
            if (IsPreservedEquipmentGroup(entry.GroupId))
            {
                kept.Add(entry);
            }
        }

        stripped.LoadSparse(kept.Count > 0 ? [.. kept] : ReadOnlySpan<KeyedSlotEntry>.Empty);
        return stripped;
    }

    private static bool IsPreservedEquipmentGroup(string? groupId)
    {
        return string.Equals(groupId, SlotGroups.WEARABLES, StringComparison.Ordinal)
            || string.Equals(groupId, SlotGroups.AUGMENTS, StringComparison.Ordinal);
    }

    private void CollectDroppableItems(in InventoryComponent inventory, List<ItemData> destination)
    {
        destination.Clear();
        KeyedSlotEntry[] entries = inventory.ToSparseEntries();
        for (var i = 0; i < entries.Length; i++)
        {
            KeyedSlotEntry entry = entries[i];
            if (entry.Item.IsEmpty())
            {
                continue;
            }

            if (!string.Equals(entry.GroupId, SlotGroups.HOTBAR, StringComparison.Ordinal)
                && !string.Equals(entry.GroupId, SlotGroups.BACKPACK, StringComparison.Ordinal))
            {
                continue;
            }

            ItemData clamped = ClampItemStack(in entry.Item);
            if (!clamped.IsEmpty())
            {
                destination.Add(clamped);
            }
        }
    }

    private bool TrySpawnDeathContainer(
        DataStore store,
        int playerEntity,
        Vector3 deathPosition,
        List<ItemData> droppedItems,
        out Vector3 containerPosition,
        out int spawnedStructureEntity
    )
    {
        containerPosition = deathPosition;
        spawnedStructureEntity = Entity.Null;

        if (brickEntities == null || brickRegistry == null || brickDatabase == null)
        {
            return false;
        }

        if (!TryLoadDeathContainerRegion(out VoxelRegionData decodedRegion))
        {
            return false;
        }

        VoxelObject centered = VoxelRegionApplier.ToCenteredVoxelObject(in decodedRegion, out Int3 centerOffset);
        if (!VoxelRegionApplier.HasSolidVoxels(centered))
        {
            return false;
        }

        containerPosition = ResolveSafeDropPosition(playerEntity, deathPosition, centerOffset, in decodedRegion);

        Uuid structureUuid;
        do
        {
            structureUuid = Uuid.NewUuid();
        }
        while (structureUuid == Uuid.Null || store.TryGet(structureUuid, out _));

        var rawEntityData = new VoxelEntityData(
            structureUuid.ToValue(),
            containerPosition.X,
            containerPosition.Y,
            containerPosition.Z,
            0f,
            0f,
            0f,
            1f,
            1f,
            1f,
            1f,
            centered.GetChunkInfos(),
            _BrickPalette: null,
            _PaintPalette: null);

        Entity spawned = VoxelWorldEntityFactory.CreateAuthority(store, in rawEntityData, brickDatabase);
        if (spawned.Ptr == Entity.Null || !store.TryGet(spawned.Ptr, out VoxelWorldComponent spawnedWorld))
        {
            return false;
        }

        brickEntities.SyncStructure(store, structureUuid, spawnedWorld.VoxelObject);
        int cargoBrickEntity = FindCargoBrickEntity(store, structureUuid);
        if (cargoBrickEntity == Entity.Null
            || !store.TryGet(cargoBrickEntity, out InventoryComponent containerInventory))
        {
            brickEntities.OnStructureRemoved(store, structureUuid);
            store.Free(spawned.Ptr);
            return false;
        }

        int currentCapacity = containerInventory.GetGroupCapacity(SlotGroups.CARGO);
        int requiredCapacity = Math.Max(currentCapacity, droppedItems.Count);
        InventoryComponent updatedContainer = containerInventory.Clone();
        if (requiredCapacity > currentCapacity && !updatedContainer.ResizeGroup(SlotGroups.CARGO, requiredCapacity))
        {
            brickEntities.OnStructureRemoved(store, structureUuid);
            store.Free(spawned.Ptr);
            return false;
        }

        for (var i = 0; i < droppedItems.Count; i++)
        {
            var slot = new SlotId(SlotGroups.CARGO, i);
            if (!updatedContainer.Set(in slot, droppedItems[i]))
            {
                brickEntities.OnStructureRemoved(store, structureUuid);
                store.Free(spawned.Ptr);
                return false;
            }
        }

        store.AddOrUpdate(cargoBrickEntity, updatedContainer);
        if (!brickEntities.MarkAsDeathContainer(store, cargoBrickEntity))
        {
            brickEntities.OnStructureRemoved(store, structureUuid);
            store.Free(spawned.Ptr);
            return false;
        }

        spawnedStructureEntity = spawned.Ptr;
        return true;
    }

    private int FindCargoBrickEntity(DataStore store, Uuid structureUuid)
    {
        if (brickEntities == null)
        {
            return Entity.Null;
        }

        int[] entities = brickEntities.GetEntitiesForStructure(structureUuid);
        for (var i = 0; i < entities.Length; i++)
        {
            int candidate = entities[i];
            if (store.TryGet(candidate, out InventoryComponent inv) && inv.HasGroup(SlotGroups.CARGO))
            {
                return candidate;
            }
        }

        return Entity.Null;
    }

    private bool TryLoadDeathContainerRegion(out VoxelRegionData decodedRegion)
    {
        decodedRegion = default;
        if (brickRegistry == null)
        {
            return false;
        }

        string blueprintId = gameplayConfig.Survival.DeathContainerBlueprintId.Get();
        if (bundledBlueprints != null)
        {
            if (string.IsNullOrWhiteSpace(blueprintId))
            {
                logger?.LogWarning("Death-Container blueprint ID is empty.");
                return false;
            }

            Result<BlueprintData> loaded = bundledBlueprints.Get(blueprintId);
            if (!loaded.Success)
            {
                logger?.LogWarning(loaded.Exception, "Failed to load Death-Container blueprint \"{id}\": {message}", blueprintId, loaded.Message);
                return false;
            }

            BlueprintData blueprint = loaded.Value;
            if (blueprint.SizeX <= 0 || blueprint.SizeY <= 0 || blueprint.SizeZ <= 0 || blueprint.Chunks == null)
            {
                logger?.LogWarning("Death-Container blueprint \"{id}\" has invalid dimensions.", blueprintId);
                return false;
            }

            VoxelRegionData encoded = blueprint.ToRegion();
            decodedRegion = VoxelCodec.Decode(in encoded, brickRegistry, paintRegistry);
            return true;
        }

        //  Headless minimal test fallback when no VFS bundled blueprint database is registered.
        ushort storageId = brickRegistry.Id(STORAGE_BRICK_ID);
        if (storageId == 0)
        {
            return false;
        }

        var voxels = new VoxelObject(VoxelObject.DEFAULT_CHUNK_EDGE);
        voxels.Set(0, 0, 0, new Voxel(storageId, new ShapeLight(BrickShape.Block, 0), 0));
        decodedRegion = new VoxelRegionData(1, 1, 1, voxels.GetChunkInfos(), _BrickPalette: null, _PaintPalette: null);
        return true;
    }

    private Vector3 ResolveSafeDropPosition(
        int ignoredPlayerEntity,
        Vector3 deathPosition,
        Int3 centerOffset,
        in VoxelRegionData decodedRegion
    )
    {
        if (physics == null)
        {
            return deathPosition;
        }

        var originTransform = new TransformComponent(deathPosition, Quaternion.Identity, Vector3.One);
        if (!RegionPlacementResolver.IntersectsWorldColliders(
            physics,
            ignoredPlayerEntity,
            in originTransform,
            centerOffset,
            in decodedRegion,
            brickDatabase))
        {
            return deathPosition;
        }

        for (var r = 0; r < _nudgeRadii.Length; r++)
        {
            float radius = _nudgeRadii[r];
            int directionLimit = r == _nudgeRadii.Length - 1 ? 6 : _nudgeDirections.Length;
            for (var d = 0; d < directionLimit; d++)
            {
                Vector3 candidate = deathPosition + _nudgeDirections[d] * radius;
                if (!IsFinite(candidate))
                {
                    continue;
                }

                var candidateTransform = new TransformComponent(candidate, Quaternion.Identity, Vector3.One);
                if (!RegionPlacementResolver.IntersectsWorldColliders(
                    physics,
                    ignoredPlayerEntity,
                    in candidateTransform,
                    centerOffset,
                    in decodedRegion,
                    brickDatabase))
                {
                    return candidate;
                }
            }
        }

        return deathPosition;
    }

    private void FreezePlayerMotion(DataStore store, int playerEntity)
    {
        var zeroMotion = new ZeroMotionAction();
        store.QueryRef<PhysicsComponent, ZeroMotionAction>(playerEntity, 0f, ref zeroMotion);

        if (store.TryGet(playerEntity, out NetworkComponent net))
        {
            net.StagedInputs?.Clear();
        }
    }

    private void DetachPlayerViewers(DataStore store, int playerEntity)
    {
        if (replication == null || sessions == null)
        {
            return;
        }

        if (sessions.TryGetClient(playerEntity, out Uuid clientId))
        {
            replication.DetachClientViewers(clientId, store);
        }
    }

    private void ApplyDeadStatistics(
        DataStore store,
        int playerEntity,
        DamageSource cause,
        Vector3 position,
        bool containerSpawned
    )
    {
        var action = new MarkDeadStatsAction(cause, position, containerSpawned);
        store.QueryRef<StatisticsComponent, MarkDeadStatsAction>(playerEntity, 0f, ref action);
    }

    private void ApplyRespawnStatistics(DataStore store, int playerEntity, in StatisticsComponent current)
    {
        float healthMax = SanitizeMax(current.GetFixed(StatisticIds.HEALTH_MAX, DefaultHealthMax), DefaultHealthMax);
        float oxygenMax = SanitizeMax(current.GetFixed(StatisticIds.OXYGEN_MAX, DefaultOxygenMax), DefaultOxygenMax);
        var action = new RespawnStatsAction(healthMax, oxygenMax);
        store.QueryRef<StatisticsComponent, RespawnStatsAction>(playerEntity, 0f, ref action);
    }

    private Vector3 ResolveSpawnPosition()
    {
        return saveService?.LevelSpawn ?? gameplayConfig.DefaultSpawnPosition;
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

    private static Vector3[] BuildNudgeDirections()
    {
        var list = new List<Vector3>(26)
        {
            Vector3.UnitY,
            -Vector3.UnitY,
            Vector3.UnitX,
            -Vector3.UnitX,
            Vector3.UnitZ,
            -Vector3.UnitZ,
        };

        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                for (var z = -1; z <= 1; z++)
                {
                    int nonZero = (x != 0 ? 1 : 0) + (y != 0 ? 1 : 0) + (z != 0 ? 1 : 0);
                    if (nonZero >= 2)
                    {
                        list.Add(Vector3.Normalize(new Vector3(x, y, z)));
                    }
                }
            }
        }

        return [.. list];
    }

    private static float SanitizeMax(float value, float fallback)
    {
        return float.IsFinite(value) && value > 0f ? value : fallback;
    }

    private static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    }

    private readonly struct ZeroMotionAction : IForEachRef<PhysicsComponent>
    {
        public void Execute(float delta, DataStore store, int entity, ref Ref<PhysicsComponent> physics)
        {
            ref readonly PhysicsComponent current = ref physics.Read;
            if (current.Velocity == Vector3.Zero && current.Torque == Vector3.Zero)
            {
                return;
            }

            ref PhysicsComponent writable = ref physics.Write;
            writable.Velocity = Vector3.Zero;
            writable.Torque = Vector3.Zero;
        }
    }

    private readonly struct MarkDeadStatsAction(DamageSource cause, Vector3 position, bool containerSpawned)
        : IForEachRef<StatisticsComponent>
    {
        public void Execute(float delta, DataStore store, int entity, ref Ref<StatisticsComponent> stats)
        {
            StatisticsComponentExtensions.ClearAllBuffs(ref stats);
            StatisticsComponentExtensions.SetFixed(ref stats, StatisticIds.HEALTH, 0f);
            StatisticsComponentExtensions.SetFlag(ref stats, StatisticIds.DEAD, true);
            StatisticsComponentExtensions.SetValue(ref stats, StatisticIds.DEATH_CAUSE, (long)cause);
            StatisticsComponentExtensions.SetFixed(ref stats, StatisticIds.DEATH_X, position.X);
            StatisticsComponentExtensions.SetFixed(ref stats, StatisticIds.DEATH_Y, position.Y);
            StatisticsComponentExtensions.SetFixed(ref stats, StatisticIds.DEATH_Z, position.Z);
            StatisticsComponentExtensions.SetFlag(ref stats, StatisticIds.DEATH_HAS_CONTAINER, containerSpawned);
        }
    }

    private readonly struct RespawnStatsAction(float healthMax, float oxygenMax) : IForEachRef<StatisticsComponent>
    {
        public void Execute(float delta, DataStore store, int entity, ref Ref<StatisticsComponent> stats)
        {
            StatisticsComponentExtensions.ClearAllBuffs(ref stats);
            StatisticsComponentExtensions.SetFixed(ref stats, StatisticIds.HEALTH, healthMax);
            StatisticsComponentExtensions.SetFixed(ref stats, StatisticIds.OXYGEN, oxygenMax);
            StatisticsComponentExtensions.SetFlag(ref stats, StatisticIds.DEAD, false);
            StatisticsComponentExtensions.Remove(ref stats, StatisticIds.DEATH_CAUSE);
            StatisticsComponentExtensions.Remove(ref stats, StatisticIds.DEATH_X);
            StatisticsComponentExtensions.Remove(ref stats, StatisticIds.DEATH_Y);
            StatisticsComponentExtensions.Remove(ref stats, StatisticIds.DEATH_Z);
            StatisticsComponentExtensions.Remove(ref stats, StatisticIds.DEATH_HAS_CONTAINER);
        }
    }
}
