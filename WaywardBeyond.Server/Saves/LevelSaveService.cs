using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Server.Components;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Networking.Components;

namespace WaywardBeyond.Server.Saves;

/// <summary>
/// Owns the authoritative voxel level on the server, keyed to a single active level, and with it the
/// server-owned per-level save database: a server is the sole author and reader of level metadata, voxel
/// entities, and per-character location. Bodies and level entities are built from the database
/// (mirroring the client's view level over the same persisted data, so entity uuids line up for
/// reconcile). On a level switch the previous level - including the previous player mirror - is fully
/// unloaded and its physics bodies disposed before the new level is built.
/// </summary>
public sealed class LevelSaveService : IDisposable
{
    private readonly ILogger _logger;
    private readonly ILevelCatalog _levelCatalog;
    private readonly IBrickRegistry _brickRegistry;
    private readonly Lock _saveLock = new();
    private readonly ConcurrentQueue<bool> _completions = new();

    private ILevelStore? _levelStore;
    private Task _pendingSave = Task.CompletedTask;
    private bool _disposed;

    public string? CurrentLevelGuid { get; private set; }
    public Level? CurrentLevel { get; private set; }
    public Vector3 LevelSpawn { get; private set; } = PlayerBodyConfig.DEFAULT_SPAWN_POSITION;

    public LevelSaveService(
        in ILogger logger,
        in ILevelCatalog levelCatalog,
        IBrickRegistry brickRegistry
    ) {
        _logger = logger;
        _levelCatalog = levelCatalog;
        _brickRegistry = brickRegistry;
    }

    /// <summary>
    /// Persists a character's location in a level, sampled from authoritative server state, so a
    /// returning player resumes where they left off. Falling back to the level spawn happens at spawn
    /// resolution. No-op when no level is loaded.
    /// </summary>
    public void SaveLocation(string levelGuid, ulong characterUuid, in Vector3 position, in Quaternion orientation)
    {
        ILevelStore? levelStore = _levelStore;
        if (levelStore == null)
        {
            return;
        }

        var data = new CharacterEntityData(
            _Uuid: 0,
            position.X,
            position.Y,
            position.Z,
            orientation.X,
            orientation.Y,
            orientation.Z,
            orientation.W,
            _ScaleX: 1,
            _ScaleY: 1,
            _ScaleZ: 1,
            _GameMode: 0
        );

        try
        {
            levelStore.WriteLocation(characterUuid, data.Serialize());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist location for character {character} in level {level}.", characterUuid, levelGuid);
        }
    }

    /// <summary>
    ///     Unloads the current level (if any) and loads <paramref name="levelGuid"/> from its save
    ///     database. Loading the same level that is already loaded is a no-op: rebuilding would free
    ///     every existing <see cref="NetworkComponent"/> entity, which with multiple clients also
    ///     destroys the player mirrors of everyone already in the level. A repeated single-player join
    ///     instead relies on the session being cleared, and multiplayer shares the loaded level.
    /// </summary>
    public bool LoadLevel(string levelGuid, in DataStore store)
    {
        if (CurrentLevelGuid == levelGuid)
        {
            return true;
        }

        Unload(store);

        ILevelStore? levelStore = _levelCatalog.Open(levelGuid);
        if (levelStore == null)
        {
            _logger.LogWarning("Tried to load level \"{level}\" but found no save database.", levelGuid);
            return false;
        }

        byte[]? levelData = levelStore.ReadLevel();
        if (levelData == null || levelData.Length == 0)
        {
            _logger.LogWarning("Tried to load level \"{level}\" but found no level metadata.", levelGuid);
            levelStore.Dispose();
            return false;
        }

        Level level = Level.Deserialize(levelData);
        if (!GameSaveMigrations.Migrator.IsSupported<Level>(level.Version.DataVersion))
        {
            _logger.LogError(
                "Refusing to load level \"{level}\" stamped with data version {version}: it was created by a newer build.",
                levelGuid, level.Version.DataVersion);
            levelStore.Dispose();
            return false;
        }

        lock (_saveLock)
        {
            _levelStore = levelStore;
        }

        CurrentLevel = level;
        LevelSpawn = new Vector3(level.SpawnX, level.SpawnY, level.SpawnZ);

        //  Data version of the level governs every structure it persists (structures carry no version of
        //  their own). Migrate each one forward, then resolve it into the in-memory local id space.
        uint levelVersion = level.Version.DataVersion;
        foreach (LevelEntityRecord record in levelStore.ReadEntities())
        {
            try
            {
                VoxelEntityData voxelEntityData = VoxelEntityData.Deserialize(record.Data);
                VoxelEntityData migrated = GameSaveMigrations.Migrator.Migrate(voxelEntityData, levelVersion);
                VoxelEntityData local = VoxelEntityDataCodec.DecodeToLocal(in migrated, _brickRegistry);
                VoxelWorldEntityFactory.CreateAuthority(store, local);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to build authority voxel entity {uuid}.", record.Uuid);
            }
        }

        CurrentLevelGuid = levelGuid;
        _logger.LogInformation("Loaded level \"{level}\" for the authoritative level.", levelGuid);
        return true;
    }

    /// <summary>
    /// Frees every server entity carrying a <see cref="NetworkComponent"/> (players and level entities),
    /// disposing their physics bodies first so teardown is marshalled to the physics thread.
    /// </summary>
    public void Unload(in DataStore store)
    {
        UnloadAction action = new();
        store.Query<NetworkComponent, UnloadAction>(0f, ref action);

        lock (_saveLock)
        {
            _levelStore?.Dispose();
            _levelStore = null;
        }

        CurrentLevelGuid = null;
        CurrentLevel = null;
        LevelSpawn = PlayerBodyConfig.DEFAULT_SPAWN_POSITION;
    }

    /// <summary>
    /// Resolves the spawn transform for a character: the persisted per-character location in the level if
    /// one exists (written by the authoritative server), otherwise the level's spawn point.
    /// </summary>
    public bool TryGetSpawnPoint(string levelGuid, ulong characterUuid, in DataStore store, out Vector3 position, out Quaternion orientation)
    {
        byte[]? locationData = _levelStore?.ReadLocation(characterUuid);
        if (locationData != null && locationData.Length > 0)
        {
            CharacterEntityData location = CharacterEntityData.Deserialize(locationData);
            position = new Vector3((float)location.X, (float)location.Y, (float)location.Z);
            orientation = new Quaternion(location.OrientationX, location.OrientationY, location.OrientationZ, location.OrientationW);
            return true;
        }

        position = LevelSpawn;
        orientation = Quaternion.Identity;
        return false;
    }

    /// <summary>
    /// Captures the authoritative level state on the caller (server) thread, then submits the blocking
    /// save-database writes to a worker. Reading the store must stay on the server thread; the writes
    /// must not block the server tick loop, so they are split. No-op when no level is loaded.
    /// </summary>
    public void QueueSave(in DataStore store)
    {
        if (string.IsNullOrEmpty(CurrentLevelGuid) || _levelStore == null)
        {
            return;
        }

        CapturedSave save = Capture(store);
        lock (_saveLock)
        {
            _pendingSave = Task.Run(() => Persist(save));
        }

        _logger.LogInformation("Queued a server level save ({entities} entities) for level \"{level}\".", save.Entities.Length, CurrentLevelGuid);
    }

    /// <summary>
    ///     Drains one finished full level save. The value is the disk-write outcome, so the server can
    ///     report completion after the background worker finishes. Other write paths never enqueue.
    /// </summary>
    public bool TryDequeueCompletion(out bool success)
    {
        return _completions.TryDequeue(out success);
    }

    /// <summary>
    /// Synchronously captures and persists the authoritative level state. Intended for shutdown: it must
    /// run after the server thread has stopped (so the store is not concurrently mutated) and before the
    /// world containers are disposed - the sequencing point that guards the shutdown cascade.
    /// </summary>
    public void Flush(in DataStore store)
    {
        if (string.IsNullOrEmpty(CurrentLevelGuid) || _levelStore == null)
        {
            return;
        }

        AwaitPendingSave();
        CapturedSave save = Capture(store);
        lock (_saveLock)
        {
            PersistLocked(save);
        }

        _logger.LogInformation("Flushed server level save for level \"{level}\".", CurrentLevelGuid);
    }

    public void Dispose()
    {
        AwaitPendingSave();

        lock (_saveLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _levelStore?.Dispose();
            _levelStore = null;
        }
    }

    /// <summary>
    /// Marks the active level as played-by-now: stamps its "last played" at the current wall-clock and
    /// persists just the level metadata. Called on join, so the save's server-owned last-played reflects
    /// whenever anyone joined it. The in-memory stamp is synchronous (so <see cref="CurrentLevel"/> is
    /// immediately fresh for load consumers); the database write is submitted to a worker so the caller
    /// (server) thread never blocks on the store. No-op when no level is loaded.
    /// </summary>
    public void MarkActive()
    {
        if (CurrentLevelGuid == null || CurrentLevel == null)
        {
            return;
        }

        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Level level = CurrentLevel.Value with { LastPlayedMs = nowMs };
        CurrentLevel = level;
        QueuePersistLevelMeta(level);
    }

    /// <summary>
    /// Accumulates the active level's "time played" up to the current wall-clock and persists just the
    /// level metadata. Called when a player leaves (or abruptly disconnects) so the save's server-owned
    /// total reflects the session. A continuous play session also accumulates through
    /// <see cref="QueueSave"/>, whose capture stamps the metadata. The in-memory stamp is synchronous
    /// and the database write is submitted to a worker, as in <see cref="MarkActive"/>. No-op when no
    /// level is loaded.
    /// </summary>
    public void EndSessionStamp()
    {
        if (CurrentLevelGuid == null || CurrentLevel == null)
        {
            return;
        }

        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Level level = StampMeta(CurrentLevel.Value, nowMs);
        CurrentLevel = level;
        QueuePersistLevelMeta(level);
    }

    private void AwaitPendingSave()
    {
        Task pending;
        lock (_saveLock)
        {
            pending = _pendingSave;
        }

        try
        {
            pending.Wait();
        }
        catch (AggregateException)
        {
            //  Persist logs its own failures; the task should not fault.
        }
    }

    private void QueuePersistLevelMeta(in Level level)
    {
        Level levelCopy = level;
        lock (_saveLock)
        {
            _pendingSave = Task.Run(() => PersistLevelMeta(levelCopy));
        }
    }

    private void PersistLevelMeta(in Level level)
    {
        lock (_saveLock)
        {
            if (_levelStore == null)
            {
                return;
            }

            try
            {
                _levelStore.WriteLevel(level.Serialize());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist level metadata for \"{guid}\".", level.Guid);
            }
        }
    }

    private static Level StampMeta(in Level level, long nowMs)
    {
        return level with
        {
            AgeMs = SaveTime.Accumulate(level.AgeMs, level.LastPlayedMs, nowMs),
            LastPlayedMs = nowMs,
        };
    }

    /// <summary>
    /// Samples structures and player locations from the authoritative server level into a serialized
    /// capture. Must be called on the server thread. Structure transforms reflect authoritative dynamics;
    /// player locations are the server-side transform, never the client's. The actively loaded level's
    /// metadata is stamped (time played) and included so a save persists its server-owned playtime total.
    /// </summary>
    private CapturedSave Capture(in DataStore store)
    {
        string levelGuid = CurrentLevelGuid!;

        //  Stamp and carry the level metadata so the save persists the aggregate playtime.
        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        CurrentLevel = StampMeta(CurrentLevel!.Value, nowMs);

        var entities = new List<LevelEntityRecord>();
        CaptureStructureAction structureAction = new()
        {
            Entries = entities,
            BrickRegistry = _brickRegistry,
        };
        store.Query<VoxelEntityDataComponent, TransformComponent, CaptureStructureAction>(0f, ref structureAction);

        var locations = new List<LevelLocationRecord>();
        CaptureLocationAction locationAction = new()
        {
            Entries = locations,
        };
        store.Query<OwnedCharacterComponent, TransformComponent, CaptureLocationAction>(0f, ref locationAction);

        return new CapturedSave(CurrentLevel.Value.Serialize(), entities.ToArray(), locations.ToArray());
    }

    private void Persist(in CapturedSave save)
    {
        bool success;
        lock (_saveLock)
        {
            success = PersistLocked(save);
        }

        _completions.Enqueue(success);
    }

    private bool PersistLocked(in CapturedSave save)
    {
        if (_levelStore == null)
        {
            return false;
        }

        try
        {
            _levelStore.WriteSave(save.LevelData, save.Entities, save.Locations);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist level save for level \"{level}\".", CurrentLevelGuid);
            return false;
        }
    }

    private struct CaptureStructureAction : IForEach<VoxelEntityDataComponent, TransformComponent>
    {
        public List<LevelEntityRecord> Entries;
        public IBrickRegistry BrickRegistry;

        public void Execute(float delta, DataStore store, int entity, in VoxelEntityDataComponent data, in TransformComponent transform)
        {
            Uuid uuid = store.GetUuid(entity);
            var voxel = VoxelEntityDataCodec.EncodeToPalette(new VoxelEntityData(
                uuid.ToValue(),
                transform.Position.X,
                transform.Position.Y,
                transform.Position.Z,
                transform.Orientation.X,
                transform.Orientation.Y,
                transform.Orientation.Z,
                transform.Orientation.W,
                transform.Scale.X,
                transform.Scale.Y,
                transform.Scale.Z,
                data.Chunks,
                _BrickPalette: null
            ), BrickRegistry);

            Entries.Add(new LevelEntityRecord(uuid.ToValue(), voxel.Serialize()));
        }
    }

    private struct CaptureLocationAction : IForEach<OwnedCharacterComponent, TransformComponent>
    {
        public List<LevelLocationRecord> Entries;

        public void Execute(float delta, DataStore store, int entity, in OwnedCharacterComponent owned, in TransformComponent transform)
        {
            Uuid uuid = store.GetUuid(entity);
            var data = new CharacterEntityData(
                uuid.ToValue(),
                transform.Position.X,
                transform.Position.Y,
                transform.Position.Z,
                transform.Orientation.X,
                transform.Orientation.Y,
                transform.Orientation.Z,
                transform.Orientation.W,
                _ScaleX: 1,
                _ScaleY: 1,
                _ScaleZ: 1,
                _GameMode: 0
            );

            Entries.Add(new LevelLocationRecord(owned.CharacterUuid, data.Serialize()));
        }
    }

    private readonly struct CapturedSave(in byte[] levelData, in LevelEntityRecord[] entities, in LevelLocationRecord[] locations)
    {
        public readonly byte[] LevelData = levelData;
        public readonly LevelEntityRecord[] Entities = entities;
        public readonly LevelLocationRecord[] Locations = locations;
    }

    private struct UnloadAction : IForEach<NetworkComponent>
    {
        public void Execute(float delta, DataStore store, int entity, in WaywardBeyond.Networking.Components.NetworkComponent component)
        {
            if (store.TryGet(entity, out PhysicsComponent physics))
            {
                physics.Dispose();
            }

            store.Free(entity);
        }
    }
}
