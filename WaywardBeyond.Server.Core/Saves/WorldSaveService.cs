using System;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.Util;
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

    private static readonly WaywardBeyond.Shared.Data.Version _gameVersion = new(_DataVersion: 3, _Name: "Wayward Beyond", _Environment: "Development");

    private readonly ILogger _logger;
    private readonly Func<KeyValueStore> _keyValueStore;

    public string? CurrentLevelGuid { get; private set; }
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

        if (!int.TryParse(seed, out int seedValue))
        {
            return false;
        }

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

        GeneratedVoxelEntity[] entities = new WorldGenerator(seedValue).Generate();

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
    /// Unloads the current world (if any) and loads <paramref name="levelGuid"/> from the <c>levels</c>
    /// bucket. Always rebuilds the world so a repeated join for the same level does not leak duplicate
    /// player mirrors from the previous session.
    /// </summary>
    public bool LoadLevel(string levelGuid, in DataStore store)
    {
        Unload(store);

        KeyValueStore kv = _keyValueStore();

        Result<byte[]> metaResult = kv.Get<byte[]>(BUCKET_NAME, levelGuid);
        if (!metaResult.Success || metaResult.Value == null || metaResult.Value.Length == 0)
        {
            _logger.LogWarning("Tried to load level \"{level}\" but found no level metadata.", levelGuid);
            return false;
        }

        Level level = Level.Deserialize(metaResult.Value);
        LevelSpawn = new Vector3(level.SpawnX, level.SpawnY, level.SpawnZ);

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
                    VoxelWorldEntityFactory.CreateAuthority(store, voxelEntityData);
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

    private static VoxelEntityData ToVoxelEntityData(in GeneratedVoxelEntity entity)
    {
        return new VoxelEntityData(
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
            entity.Chunks
        );
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