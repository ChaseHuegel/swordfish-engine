using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Krill;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Server.Components;
using WaywardBeyond.Bricks;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Levels;
using WaywardBeyond.Gameplay.Networking;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Server.Saves;

/// <summary>
/// Owns the authoritative voxel level on the server, keyed to a single active level, and with it the
/// server-owned per-level save database: a server is the sole author and reader of level metadata, voxel
/// entities, and per-character location. Bodies and level entities are built from the database
/// (mirroring the client's view level over the same persisted data, so entity uuids line up for
/// reconcile). On a level switch the previous level - including the previous player mirror - is fully
/// unloaded. The engine releases the freed physics bodies before the solver runs.
/// </summary>
public sealed class LevelSaveService : IDisposable
{
    private readonly ILogger _logger;
    private readonly ILevelCatalog _levelCatalog;
    private readonly IBrickRegistry _brickRegistry;
    private readonly IBrickDatabase _brickDatabase;
    private readonly IPaintRegistry? _paintRegistry;
    private readonly Lock _saveLock = new();
    private readonly ConcurrentQueue<bool> _completions = new();
    private readonly Dictionary<ulong, PendingCharacterLocation> _pendingCharacterLocations = [];

    private ILevelStorage? _levelStorage;
    private Task _pendingSave = Task.CompletedTask;
    private long _nextCharacterSaveSequence;
    private bool _disposed;

    /// <summary>The loaded level guid, or null when no level is loaded.</summary>
    public string? CurrentLevelGuid { get; private set; }
    /// <summary>The loaded level metadata, or null when no level is loaded.</summary>
    public Level? CurrentLevel { get; private set; }
    /// <summary>The level spawn point, or the configured default when no level is loaded.</summary>
    public Vector3 LevelSpawn { get; private set; }

    private readonly GameplayConfig _gameplayConfig;

    /// <summary>Creates the save service for one world. Each world owns its own instance and handle.</summary>
    public LevelSaveService(
        ILogger logger,
        ILevelCatalog levelCatalog,
        IBrickRegistry brickRegistry,
        IBrickDatabase brickDatabase,
        GameplayConfig gameplayConfig,
        IPaintRegistry? paintRegistry = null
    ) {
        _logger = logger;
        _levelCatalog = levelCatalog;
        _brickRegistry = brickRegistry;
        _brickDatabase = brickDatabase;
        _paintRegistry = paintRegistry;
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
                VoxelEntityData local = VoxelCodec.Decode(in entity, _brickRegistry, _paintRegistry);
                VoxelWorldEntityFactory.CreateAuthority(store, local, _brickDatabase);
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
    /// Frees every server entity carrying a <see cref="NetworkComponent"/> (players and level entities).
    /// The engine releases their physics bodies before the solver runs.
    /// </summary>
    public void Unload(DataStore store)
    {
        UnloadAction action = new();
        store.Query<NetworkComponent, UnloadAction>(0f, ref action);

        //  A queued capture still targets the storage it came from, so drain it before the handle closes.
        AwaitPendingSave();

        lock (_saveLock)
        {
            _pendingCharacterLocations.Clear();
            _levelStorage?.Dispose();
            _levelStorage = null;
        }

        CurrentLevelGuid = null;
        CurrentLevel = null;
        LevelSpawn = _gameplayConfig.DefaultSpawnPosition;
    }

    /// <summary>Gets the character's pending or persisted entity state for the active level.</summary>
    public Result<CharacterEntityData> GetCharacterEntity(ulong characterUuid)
    {
        lock (_saveLock)
        {
            if (_pendingCharacterLocations.TryGetValue(characterUuid, out PendingCharacterLocation pending))
            {
                return Result<CharacterEntityData>.FromSuccess(pending.Data);
            }
        }

        return _levelStorage?.GetCharacterEntity(characterUuid) ?? Result<CharacterEntityData>.FromFailure("No level loaded.");
    }

    /// <summary>
    /// Resolves the spawn transform for a character: the persisted per-character location in the level if
    /// one exists (written by the authoritative server), otherwise the level's spawn point.
    /// </summary>
    public bool TryGetSpawnPoint(ulong characterUuid, out Vector3 position, out Quaternion orientation)
    {
        Result<CharacterEntityData> locationResult = GetCharacterEntity(characterUuid);
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

    /// <summary>Captures one character's location on the server thread and queues its write to the level database.</summary>
    public void QueueSaveCharacterLocation(DataStore store, int entity)
    {
        ILevelStorage? storage = _levelStorage;
        if (string.IsNullOrEmpty(CurrentLevelGuid) || storage == null)
        {
            return;
        }

        if (!store.TryGet(entity, out TransformComponent transform))
        {
            return;
        }

        CharacterEntityData data = CaptureCharacter(store, entity, in transform);
        lock (_saveLock)
        {
            long sequence = ++_nextCharacterSaveSequence;
            _pendingCharacterLocations[data.Uuid] = new PendingCharacterLocation(data, sequence);
            _pendingSave = _pendingSave.ContinueWith(_ => PersistCharacterLocation(storage, data, sequence));
        }
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

    /// <inheritdoc/>
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
        ILevelStorage? storage = _levelStorage;
        if (storage == null)
        {
            return;
        }

        Level levelCopy = level;
        lock (_saveLock)
        {
            _pendingSave = _pendingSave.ContinueWith(_ => PersistLevelMeta(storage, levelCopy));
        }
    }

    private void PersistLevelMeta(ILevelStorage storage, in Level level)
    {
        lock (_saveLock)
        {
            Result result = storage.Save(level);
            if (!result.Success)
            {
                _logger.LogError(result.Exception, "Failed to persist level metadata for \"{guid}\": {message}", level.Guid, result.Message);
            }
        }
    }

    private void PersistCharacterLocation(ILevelStorage storage, in CharacterEntityData data, long sequence)
    {
        try
        {
            Result result = storage.Save(data);
            if (!result.Success)
            {
                _logger.LogError(result.Exception, "Failed to persist character location for {character}: {message}", data.Uuid, result.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist character location for {character}.", data.Uuid);
        }
        finally
        {
            lock (_saveLock)
            {
                if (_pendingCharacterLocations.TryGetValue(data.Uuid, out PendingCharacterLocation pending)
                    && pending.Sequence == sequence)
                {
                    _pendingCharacterLocations.Remove(data.Uuid);
                }
            }
        }
    }

    private static CharacterEntityData CaptureCharacter(DataStore store, int entity, in TransformComponent transform)
    {
        Uuid uuid = store.GetUuid(entity);
        GameMode gameMode = store.TryGet(entity, out GameModeComponent mode) ? mode.Mode : GameMode.Adventure;
        return new CharacterEntityData(
            uuid.ToValue(),
            transform.Position.X,
            transform.Position.Y,
            transform.Position.Z,
            transform.Orientation.X,
            transform.Orientation.Y,
            transform.Orientation.Z,
            transform.Orientation.W,
            _ScaleX: transform.Scale.X,
            _ScaleY: transform.Scale.Y,
            _ScaleZ: transform.Scale.Z,
            _GameMode: gameMode
        );
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
        ILevelStorage storage = _levelStorage
            ?? throw new InvalidOperationException("A level must be loaded before it can be captured.");

        //  Stamp and carry the level metadata so the save persists the aggregate playtime.
        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        CurrentLevel = StampMeta(CurrentLevel!.Value, nowMs);

        var entities = new List<VoxelEntityData>();
        CaptureStructureAction structureAction = new()
        {
            Entries = entities,
            BrickRegistry = _brickRegistry,
            PaintRegistry = _paintRegistry,
        };
        store.Query<VoxelWorldComponent, TransformComponent, CaptureStructureAction>(0f, ref structureAction);

        var locations = new List<CharacterEntityData>();
        CaptureLocationAction locationAction = new()
        {
            Entries = locations,
        };
        store.Query<OwnedCharacterComponent, TransformComponent, CaptureLocationAction>(0f, ref locationAction);

        return new CapturedSave(storage, CurrentLevel.Value, entities.ToArray(), locations.ToArray());
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
        try
        {
            //  The capture owns the storage it was taken from, so a queued save can never land in
            //  whichever level happens to be loaded when the worker runs.
            Result result = save.Storage.Save(save.Level, save.Entities, save.Locations);
            return result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist level save for level \"{level}\".", save.Level.Guid);
            return false;
        }
    }

    private struct CaptureStructureAction : IForEach<VoxelWorldComponent, TransformComponent>
    {
        public List<VoxelEntityData> Entries;
        public IBrickRegistry BrickRegistry;
        public IPaintRegistry? PaintRegistry;

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
                _BrickPalette: null,
                _PaintPalette: null
            );
            
            voxelEntityData = VoxelCodec.Encode(voxelEntityData, BrickRegistry, PaintRegistry);
            Entries.Add(voxelEntityData);
        }
    }

    private struct CaptureLocationAction : IForEach<OwnedCharacterComponent, TransformComponent>
    {
        public List<CharacterEntityData> Entries;

        public void Execute(float delta, DataStore store, int entity, in OwnedCharacterComponent owned, in TransformComponent transform)
        {
            Entries.Add(CaptureCharacter(store, entity, in transform));
        }
    }

    private readonly struct PendingCharacterLocation(in CharacterEntityData data, long sequence)
    {
        public readonly CharacterEntityData Data = data;
        public readonly long Sequence = sequence;
    }

    private readonly struct CapturedSave(in ILevelStorage storage, in Level level, in VoxelEntityData[] entities, in CharacterEntityData[] locations)
    {
        public readonly ILevelStorage Storage = storage;
        public readonly Level Level = level;
        public readonly VoxelEntityData[] Entities = entities;
        public readonly CharacterEntityData[] Locations = locations;
    }

    private struct UnloadAction : IForEach<NetworkComponent>
    {
        public void Execute(float delta, DataStore store, int entity, in NetworkComponent component)
        {
            store.Free(entity);
        }
    }
}
