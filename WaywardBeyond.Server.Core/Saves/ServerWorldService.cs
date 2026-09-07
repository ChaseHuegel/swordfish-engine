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
/// Owns the authoritative voxel world on the server, keyed to a single active level. Bodies and world
/// entities are built from the <c>levels</c> KV bucket (mirroring the client's view world over the same
/// persisted data, so entity uuids line up for reconcile). On a level switch the previous world - including
/// the previous player mirror - is fully unloaded and its physics bodies disposed before the new world is
/// built. Designed against the Phase-2 interim (a server-side read of <c>levels</c>); Phase 4 replaces this
/// with the server-owned <c>WorldSaveService</c> and join streaming.
/// </summary>
public sealed class ServerWorldService
{
    private const string BUCKET_NAME = "levels";

    private readonly ILogger _logger;
    private readonly Func<KeyValueStore> _keyValueStore;

    public string? CurrentLevelGuid { get; private set; }
    public Vector3 LevelSpawn { get; private set; } = PlayerBodyConfig.DEFAULT_SPAWN_POSITION;

    public ServerWorldService(
        in ILogger logger,
        in Func<KeyValueStore> keyValueStore
    ) {
        _logger = logger;
        _keyValueStore = keyValueStore;
    }

    /// <summary>
    /// Unloads the current world (if any) and loads <paramref name="levelGuid"/> from the <c>levels</c>
    /// bucket. Always rebuilds the world so a repeated spawn for the same level does not leak duplicate
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
        _logger.LogInformation("Loaded level \"{level}\" with {entities} world entities.", levelGuid, CurrentLevelGuid);
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
    /// one exists (written by the client's save), otherwise the level's <see cref="Level"/> spawn point.
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