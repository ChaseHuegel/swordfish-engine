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
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Levels;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Networking.Components;
using WaywardBeyond.Voxels;

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

    private ILevelStorage? _levelStorage;
    private Task _pendingSave = Task.CompletedTask;
    private bool _disposed;

    public string? CurrentLevelGuid { get; private set; }
    public Level? CurrentLevel { get; private set; }
    public Vector3 LevelSpawn { get; private set; }

    private readonly GameplayConfig _gameplayConfig;

    public LevelSaveService(
        ILogger logger,
        ILevelCatalog levelCatalog,
        IBrickRegistry brickRegistry,
        GameplayConfig gameplayConfig
    ) {
        _logger = logger;
        _levelCatalog = levelCatalog;
        _brickRegistry = brickRegistry;
        _gameplayConfig = gameplayConfig;
        LevelSpawn = _gameplayConfig.DefaultSpawnPosition;
    }

    /// <summary>
    ///     Unloads the current level (if any) and loads <paramref name="levelGuid"/> from its save
    ///     database. Loading the same level that is already loaded is a no-op: rebuilding would free
    ///     every existing <see cref="NetworkComponent"/> entity, which with multiple clients also
    ///     destroys the player mirrors of everyone already in the level. A repeated single-player join
    ///     instead relies on the session being cleared, and multiplayer shares the loaded level.
    /// </summary>
    public bool LoadLevel(string levelGuid, DataStore store)
    {
        if (CurrentLevelGuid == levelGuid)
        {
            return true;
        }

        Unload(store);

        ILevelStorage? levelStorage = _levelCatalog.Open(levelGuid);
        if (levelStorage == null)
        {
            _logger.LogWarning("Tried to load level \"{level}\" but found no save database.", levelGuid);
            return false;
        }

        Result<Level> levelResult = levelStorage.GetLevel();
        if (!levelResult.Success)
        {
            _logger.LogWarning("Tried to load level \"{level}\" but found no readable level metadata: {message}", levelGuid, levelResult.Message);
            levelStorage.Dispose();
            return false;
        }

        Level level = levelResult.Value;

        lock (_saveLock)
        {
            _levelStorage = levelStorage;
        }

        CurrentLevel = level;
        LevelSpawn = new Vector3(level.SpawnX, level.SpawnY, level.SpawnZ);

        //  Resolve each persisted structure into the in-memory local id space. The store has already
        //  deserialized and migrated every entity to the level's data version.
        foreach (VoxelEntityData entity in levelStorage.GetVoxelEntities(level.Version.DataVersion))
        {
            try
            {
                VoxelEntityData local = VoxelCodec.Decode(in entity, _brickRegistry);
                VoxelWorldEntityFactory.CreateAuthority(store, local);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to build authority voxel entity {uuid}.", entity.Uuid);
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
    public void Unload(DataStore store)
    {
        UnloadAction action = new();
        store.Query<NetworkComponent, UnloadAction>(0f, ref action);

        lock (_saveLock)
        {
            _levelStorage?.Dispose();
            _levelStorage = null;
        }

        CurrentLevelGuid = null;
        CurrentLevel = null;
        LevelSpawn = _gameplayConfig.DefaultSpawnPosition;
    }

    /// <summary>
    /// Resolves the spawn transform for a character: the persisted per-character location in the level if
    /// one exists (written by the authoritative server), otherwise the level's spawn point.
    /// </summary>
    public bool TryGetSpawnPoint(ulong characterUuid, out Vector3 position, out Quaternion orientation)
    {
        Result<CharacterEntityData> locationResult = _levelStorage?.GetCharacterEntity(characterUuid) ?? Result<CharacterEntityData>.FromFailure("No level loaded.");
        if (locationResult.Success)
        {
            CharacterEntityData location = locationResult.Value;
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
    public void QueueSave(DataStore store)
    {
        if (string.IsNullOrEmpty(CurrentLevelGuid) || _levelStorage == null)
        {
            return;
        }

        CapturedSave save = Capture(store);
        lock (_saveLock)
        {
            //  Chain behind the pending save so captures persist in enqueue order: a stale capture
            //  must never overwrite a newer one.
            _pendingSave = _pendingSave.ContinueWith(_ => Persist(save));
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
    public void Flush(DataStore store)
    {
        if (string.IsNullOrEmpty(CurrentLevelGuid) || _levelStorage == null)
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
            _levelStorage?.Dispose();
            _levelStorage = null;
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
        Level level = CurrentLevel.Value with { Playtime = CurrentLevel.Value.Playtime with { LastPlayedMs = nowMs } };
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
            _pendingSave = _pendingSave.ContinueWith(_ => PersistLevelMeta(levelCopy));
        }
    }

    private void PersistLevelMeta(in Level level)
    {
        lock (_saveLock)
        {
            if (_levelStorage == null)
            {
                return;
            }

            Result result = _levelStorage.Save(level);
            if (!result.Success)
            {
                _logger.LogError(result.Exception, "Failed to persist level metadata for \"{guid}\": {message}", level.Guid, result.Message);
            }
        }
    }

    private static Level StampMeta(in Level level, long nowMs)
    {
        return level with
        {
            Playtime = level.Playtime.Advance(nowMs)
        };
    }

    /// <summary>
    /// Samples structures and player locations from the authoritative server level into a serialized
    /// capture. Must be called on the server thread. Structure transforms reflect authoritative dynamics;
    /// player locations are the server-side transform, never the client's. The actively loaded level's
    /// metadata is stamped (time played) and included so a save persists its server-owned playtime total.
    /// </summary>
    private CapturedSave Capture(DataStore store)
    {
        //  Stamp and carry the level metadata so the save persists the aggregate playtime.
        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        CurrentLevel = StampMeta(CurrentLevel!.Value, nowMs);

        var entities = new List<VoxelEntityData>();
        CaptureStructureAction structureAction = new()
        {
            Entries = entities,
            BrickRegistry = _brickRegistry,
        };
        store.Query<VoxelWorldComponent, TransformComponent, CaptureStructureAction>(0f, ref structureAction);

        var locations = new List<CharacterEntityData>();
        CaptureLocationAction locationAction = new()
        {
            Entries = locations,
        };
        store.Query<OwnedCharacterComponent, TransformComponent, CaptureLocationAction>(0f, ref locationAction);

        return new CapturedSave(CurrentLevel.Value, entities.ToArray(), locations.ToArray());
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
        if (_levelStorage == null)
        {
            return false;
        }

        try
        {
            Result result = _levelStorage.Save(save.Level, save.Entities, save.Locations);
            return result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist level save for level \"{level}\".", CurrentLevelGuid);
            return false;
        }
    }

    private struct CaptureStructureAction : IForEach<VoxelWorldComponent, TransformComponent>
    {
        public List<VoxelEntityData> Entries;
        public IBrickRegistry BrickRegistry;

        public void Execute(float delta, DataStore store, int entity, in VoxelWorldComponent world, in TransformComponent transform)
        {
            Uuid uuid = store.GetUuid(entity);

            var voxelEntityData = new VoxelEntityData(
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
                world.VoxelObject.GetChunkInfos(),
                _BrickPalette: null
            );
            
            voxelEntityData = VoxelCodec.Encode(voxelEntityData, BrickRegistry);
            Entries.Add(voxelEntityData);
        }
    }

    private struct CaptureLocationAction : IForEach<OwnedCharacterComponent, TransformComponent>
    {
        public List<CharacterEntityData> Entries;

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

            Entries.Add(data);
        }
    }

    private readonly struct CapturedSave(in Level level, in VoxelEntityData[] entities, in CharacterEntityData[] locations)
    {
        public readonly Level Level = level;
        public readonly VoxelEntityData[] Entities = entities;
        public readonly CharacterEntityData[] Locations = locations;
    }

    private struct UnloadAction : IForEach<NetworkComponent>
    {
        public void Execute(float delta, DataStore store, int entity, in NetworkComponent component)
        {
            if (store.TryGet(entity, out PhysicsComponent physics))
            {
                physics.Dispose();
            }

            store.Free(entity);
        }
    }
}
