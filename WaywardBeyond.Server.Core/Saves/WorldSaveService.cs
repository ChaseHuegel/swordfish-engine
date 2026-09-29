using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Server.Core.Components;
using WaywardBeyond.Shared.Bricks;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Gameplay;
using WaywardBeyond.Shared.Networking.Components;

namespace WaywardBeyond.Server.Core.Saves;

/// <summary>
/// Owns the authoritative voxel world on the server, keyed to a single active level, and with it the
/// server-owned <c>levels</c> KV bucket: a server is the sole author and reader of world metadata, voxel
/// entities, and per-character location. Bodies and world entities are built from the bucket (mirroring
/// the client's view world over the same persisted data, so entity uuids line up for reconcile), and new
/// worlds can be generated server-side from just a name and seed. On a level switch the previous world -
/// including the previous player mirror - is fully unloaded and its physics bodies disposed before the
/// new world is built.
/// </summary>
public sealed class WorldSaveService
{
    private const string BUCKET_NAME = "levels";

    private static readonly WaywardBeyond.Shared.Data.Version _gameVersion = new(_DataVersion: SaveVersion.CurrentDataVersion, _Name: "Wayward Beyond", _Environment: "Development");

    private readonly ILogger _logger;
    private readonly Func<KeyValueStore> _keyValueStore;

    public string? CurrentLevelGuid { get; private set; }
    public Level? CurrentLevel { get; private set; }
    public Vector3 LevelSpawn { get; private set; } = PlayerBodyConfig.DEFAULT_SPAWN_POSITION;

    public WorldSaveService(
        in ILogger logger,
        in Func<KeyValueStore> keyValueStore
    ) {
        _logger = logger;
        _keyValueStore = keyValueStore;
    }

    /// <summary>
    /// Generates a brand new world from a name and seed and persists it to the <c>levels</c> bucket
    /// (level meta plus one <c>&lt;guid&gt;.entity.&lt;uuid&gt;</c> entry per structure). No authority
    /// bodies are built here - those arise when a player joins and the level is loaded. Runs the shared,
    /// deterministic world generator; expect to call it off the server tick thread.
    /// </summary>
    public bool CreateWorld(string name, string seed, GameMode gameMode, out string levelGuid)
    {
        levelGuid = string.Empty;
        int seedValue = WorldGenerator.HashSeed(seed);

        KeyValueStore kv = _keyValueStore();

        Guid guid = Guid.NewGuid();
        long nowUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var level = new Level(
            _gameVersion,
            seedValue,
            nowUtcMs,
            _AgeMs: 0,
            _SpawnX: 0,
            _SpawnY: 1,
            _SpawnZ: 5,
            gameMode,
            guid.ToString(),
            name
        );

        GeneratedVoxelEntity[] entities = new WorldGenerator(seedValue, BaseBrickCatalog.Registry).Generate();

        try
        {
            kv.Put(BUCKET_NAME, guid.ToString(), level.Serialize());
            foreach (GeneratedVoxelEntity entity in entities)
            {
                VoxelEntityData data = ToVoxelEntityData(entity);
                kv.Put(BUCKET_NAME, $"{guid}.entity.{entity.Uuid}", data.Serialize());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist generated world \"{name}\".", name);
            return false;
        }

        levelGuid = guid.ToString();
        _logger.LogInformation("Generated and persisted world \"{name}\" ({guid}) with {entities} structures.", name, levelGuid, entities.Length);
        return true;
    }

    /// <summary>
    /// Enumerates every saved world's level metadata for the save-listing UI.
    /// </summary>
    public Level[] ListLevels()
    {
        KeyValueStore kv = _keyValueStore();

        Result<string[]> keysResult = kv.GetKeys(BUCKET_NAME);
        if (!keysResult.Success)
        {
            return [];
        }

        var levels = new System.Collections.Generic.List<Level>();
        foreach (string key in keysResult.Value)
        {
            if (!Guid.TryParse(key, out _))
            {
                continue;
            }

            Result<byte[]> metaResult = kv.Get<byte[]>(BUCKET_NAME, key);
            if (!metaResult.Success || metaResult.Value.Length == 0)
            {
                continue;
            }

            try
            {
                Level level = Level.Deserialize(metaResult.Value);
                if (!GameSaveMigrations.Migrator.IsSupported(level.Version.DataVersion))
                {
                    _logger.LogWarning(
                        "Skipping level \"{key}\" with newer data version {version} in the save list.",
                        key, level.Version.DataVersion);
                    continue;
                }

                if (!string.IsNullOrEmpty(level.Guid))
                {
                    levels.Add(level);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize level metadata for key \"{key}\".", key);
            }
        }

        return levels.ToArray();
    }

    /// <summary>
    /// Deletes a saved world: its level meta and every <c>&lt;guid&gt;.*</c> entity/location entry.
    /// </summary>
    public void DeleteLevel(string levelGuid)
    {
        KeyValueStore kv = _keyValueStore();
        try
        {
            kv.Delete(BUCKET_NAME, levelGuid);

            Result<string[]> keysResult = kv.GetKeys(BUCKET_NAME);
            if (keysResult.Success)
            {
                var keysToDelete = new System.Collections.Generic.List<string>();
                string prefix = $"{levelGuid}.";
                foreach (string key in keysResult.Value)
                {
                    if (key.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        keysToDelete.Add(key);
                    }
                }

                if (keysToDelete.Count > 0)
                {
                    kv.Delete(BUCKET_NAME, keysToDelete.ToArray());
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete world \"{levelGuid}\".", levelGuid);
        }
    }

    /// <summary>
    /// Persists a character's location in a level, sampled from authoritative server state, so a returning
    /// player resumes where they left off. Falling back to the level spawn happens at spawn resolution.
    /// </summary>
    public void SaveLocation(string levelGuid, ulong characterId, in Vector3 position, in Quaternion orientation)
    {
        KeyValueStore kv = _keyValueStore();
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
            kv.Put(BUCKET_NAME, $"{levelGuid}.character.{characterId}", data.Serialize());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist location for character {character} in level {level}.", characterId, levelGuid);
        }
    }

    /// <summary>
    ///     Unloads the current world (if any) and loads <paramref name="levelGuid"/> from the <c>levels</c>
    ///     bucket. Loading the same level that is already loaded is a no-op: rebuilding would free every
    ///     existing <see cref="NetworkComponent"/> entity, which with multiple clients also destroys the
    ///     player mirrors of everyone already in the world. A repeated single-player join instead relies on
    ///     the session being cleared, and multiplayer shares the loaded world.
    /// </summary>
    public bool LoadLevel(string levelGuid, in DataStore store)
    {
        if (CurrentLevelGuid == levelGuid)
        {
            return true;
        }

        Unload(store);

        KeyValueStore kv = _keyValueStore();

        Result<byte[]> metaResult = kv.Get<byte[]>(BUCKET_NAME, levelGuid);
        if (!metaResult.Success || metaResult.Value == null || metaResult.Value.Length == 0)
        {
            _logger.LogWarning("Tried to load level \"{level}\" but found no level metadata.", levelGuid);
            return false;
        }

        Level level = Level.Deserialize(metaResult.Value);
        if (!GameSaveMigrations.Migrator.IsSupported(level.Version.DataVersion))
        {
            _logger.LogError(
                "Refusing to load level \"{level}\" stamped with data version {version}: it was created by a newer build.",
                levelGuid, level.Version.DataVersion);
            return false;
        }

        CurrentLevel = level;
        LevelSpawn = new Vector3(level.SpawnX, level.SpawnY, level.SpawnZ);

        //  Data version of the level governs every structure it persists (structures carry no version of
        //  their own). Migrate each one forward, then resolve it into the in-memory local id space.
        uint levelVersion = level.Version.DataVersion;
        Result<string[]> keysResult = kv.GetKeys(BUCKET_NAME);
        if (keysResult.Success)
        {
            string prefix = $"{levelGuid}.entity.";
            string[] keys = keysResult.Value;
            for (var i = 0; i < keys.Length; i++)
            {
                if (!keys[i].StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                try
                {
                    Result<byte[]> entityResult = kv.Get<byte[]>(BUCKET_NAME, keys[i]);
                    if (!entityResult.Success || entityResult.Value == null || entityResult.Value.Length == 0)
                    {
                        continue;
                    }

                    VoxelEntityData voxelEntityData = VoxelEntityData.Deserialize(entityResult.Value);
                    VoxelEntityData migrated = GameSaveMigrations.Migrator.Migrate(voxelEntityData, levelVersion);
                    VoxelEntityData local = VoxelEntityDataCodec.DecodeToLocal(in migrated, BaseBrickCatalog.Registry);
                    VoxelWorldEntityFactory.CreateAuthority(store, local);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to build authority voxel entity from key \"{key}\".", keys[i]);
                }
            }
        }

        CurrentLevelGuid = levelGuid;
        _logger.LogInformation("Loaded level \"{level}\" for the authoritative world.", levelGuid);
        return true;
    }

    /// <summary>
    /// Frees every server entity carrying a <see cref="NetworkComponent"/> (players and world entities),
    /// disposing their physics bodies first so teardown is marshalled to the physics thread.
    /// </summary>
    public void Unload(in DataStore store)
    {
        UnloadAction action = new();
        store.Query<NetworkComponent, UnloadAction>(0f, ref action);

        CurrentLevelGuid = null;
        CurrentLevel = null;
        LevelSpawn = PlayerBodyConfig.DEFAULT_SPAWN_POSITION;
    }

    /// <summary>
    /// Resolves the spawn transform for a character: the persisted per-character location in the level if
    /// one exists (written by the authoritative server), otherwise the level's spawn point.
    /// </summary>
    public bool TryGetSpawnPoint(string levelGuid, ulong characterId, in DataStore store, out Vector3 position, out Quaternion orientation)
    {
        KeyValueStore kv = _keyValueStore();

        Result<byte[]> locationResult = kv.Get<byte[]>(BUCKET_NAME, $"{levelGuid}.character.{characterId}");
        if (locationResult.Success && locationResult.Value != null && locationResult.Value.Length > 0)
        {
            CharacterEntityData location = CharacterEntityData.Deserialize(locationResult.Value);
            position = new Vector3((float)location.X, (float)location.Y, (float)location.Z);
            orientation = new Quaternion(location.OrientationX, location.OrientationY, location.OrientationZ, location.OrientationW);
            return true;
        }

        position = LevelSpawn;
        orientation = Quaternion.Identity;
        return false;
    }

    /// <summary>
    /// Captures the authoritative world state on the caller (server) thread into a list of serialized KV
    /// entries, then submits those to a worker for the blocking <c>KeyValueStore</c> writes. Reading the
    /// store must stay on the server thread; the writes must not block the server tick loop, so they are
    /// split. No-op when no level is loaded.
    /// </summary>
    public void QueueWorldSave(in DataStore store)
    {
        if (string.IsNullOrEmpty(CurrentLevelGuid))
        {
            return;
        }

        List<WorldEntry> entries = Capture(store);
        if (entries.Count == 0)
        {
            return;
        }

        var entriesCopy = entries;
        _ = Task.Run(() => Persist(entriesCopy));
        _logger.LogInformation("Queued a server world save ({entries} entries) for level \"{level}\".", entries.Count, CurrentLevelGuid);
    }

    /// <summary>
    /// Synchronously captures and persists the authoritative world state. Intended for shutdown: it must
    /// run after the server thread has stopped (so the store is not concurrently mutated) and before the
    /// local NATS / <c>KeyValueStore</c> backing is disposed - the sequencing point that guards the
    /// shutdown cascade.
    /// </summary>
    public void Flush(in DataStore store)
    {
        if (string.IsNullOrEmpty(CurrentLevelGuid))
        {
            return;
        }

        List<WorldEntry> entries = Capture(store);
        Persist(entries);
        _logger.LogInformation("Flushed server world save for level \"{level}\".", CurrentLevelGuid);
    }

    /// <summary>
    /// Marks the active level as played-by-now: stamps its "last played" at the current wall-clock and
    /// persists just the level metadata. Called on join, so the save's server-owned last-played reflects
    /// whenever anyone joined it. The in-memory stamp is synchronous (so <see cref="CurrentLevel"/> is
    /// immediately fresh for <see cref="LoadLevel"/> consumers); the KV write is submitted to a worker so
    /// the caller (server) thread never blocks on the blocking store. No-op when no level is loaded.
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
    /// <see cref="QueueWorldSave"/>, whose capture stamps the metadata. The in-memory stamp is synchronous
    /// and the KV write is submitted to a worker, as in <see cref="MarkActive"/>. No-op when no level is
    /// loaded.
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

    private void QueuePersistLevelMeta(in Level level)
    {
        Level levelCopy = level;
        _ = Task.Run(() => PersistLevelMeta(levelCopy));
    }

    private void PersistLevelMeta(in Level level)
    {
        try
        {
            _keyValueStore().Put(BUCKET_NAME, level.Guid, level.Serialize());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist level metadata for \"{guid}\".", level.Guid);
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
    /// Samples structures and player locations from the authoritative server world into serialized KV
    /// entries. Must be called on the server thread. Structure transforms reflect authoritative dynamics;
    /// player locations are the server-side transform, never the client's. The actively loaded level's
    /// metadata is stamped (time played) and included so a save persists its server-owned playtime total.
    /// </summary>
    private List<WorldEntry> Capture(in DataStore store)
    {
        var entries = new List<WorldEntry>();
        string levelGuid = CurrentLevelGuid!;

        //  Stamp and carry the level metadata so the world save persists the aggregate playtime.
        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        CurrentLevel = StampMeta(CurrentLevel!.Value, nowMs);
        entries.Add(new WorldEntry(levelGuid, CurrentLevel.Value.Serialize()));

        CaptureStructureAction structureAction = new()
        {
            LevelGuid = levelGuid,
            Entries = entries,
        };
        store.Query<VoxelEntityDataComponent, TransformComponent, CaptureStructureAction>(0f, ref structureAction);

        CaptureLocationAction locationAction = new()
        {
            LevelGuid = levelGuid,
            Entries = entries,
        };
        store.Query<OwnedCharacterComponent, TransformComponent, CaptureLocationAction>(0f, ref locationAction);

        return entries;
    }

    private void Persist(in List<WorldEntry> entries)
    {
        KeyValueStore kv = _keyValueStore();
        foreach (WorldEntry entry in entries)
        {
            try
            {
                kv.Put(BUCKET_NAME, entry.Key, entry.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist world entry \"{key}\".", entry.Key);
            }
        }
    }

    private static VoxelEntityData ToVoxelEntityData(in GeneratedVoxelEntity entity)
    {
        return VoxelEntityDataCodec.EncodeToPalette(new VoxelEntityData(
            entity.Uuid.ToValue(),
            entity.Position.X,
            entity.Position.Y,
            entity.Position.Z,
            entity.Orientation.X,
            entity.Orientation.Y,
            entity.Orientation.Z,
            entity.Orientation.W,
            _ScaleX: 1,
            _ScaleY: 1,
            _ScaleZ: 1,
            entity.Chunks,
            _BrickPalette: null
        ), BaseBrickCatalog.Registry);
    }

    private struct CaptureStructureAction : IForEach<VoxelEntityDataComponent, TransformComponent>
    {
        public string LevelGuid;
        public List<WorldEntry> Entries;

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
            ), BaseBrickCatalog.Registry);

            Entries.Add(new WorldEntry($"{LevelGuid}.entity.{uuid}", voxel.Serialize()));
        }
    }

    private struct CaptureLocationAction : IForEach<OwnedCharacterComponent, TransformComponent>
    {
        public string LevelGuid;
        public List<WorldEntry> Entries;

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

            Entries.Add(new WorldEntry($"{LevelGuid}.character.{owned.CharacterId}", data.Serialize()));
        }
    }

    private readonly struct WorldEntry(in string key, in byte[] value)
    {
        public readonly string Key = key;
        public readonly byte[] Value = value;
    }

    private struct UnloadAction : IForEach<NetworkComponent>
    {
        public void Execute(float delta, DataStore store, int entity, in WaywardBeyond.Shared.Networking.Components.NetworkComponent component)
        {
            if (store.TryGet(entity, out PhysicsComponent physics))
            {
                physics.Dispose();
            }

            store.Free(entity);
        }
    }
}
