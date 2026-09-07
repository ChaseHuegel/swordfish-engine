using System.Numerics;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Server.Core.Saves;
using WaywardBeyond.Shared.Gameplay;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Components;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Server.Core.Systems;

/// <summary>
/// Server-authoritative spawn. Handles client <see cref="SpawnRequest"/>s by allocating a player
/// entity on the server world, constructing its physics body (server owns body construction), wiring it
/// for replication, and replying with the assigned uuid. The server assigns the initial transform,
/// which replicates downstream; the client never authors authoritative state.
/// </summary>
public sealed class ServerSpawnSystem : IEntitySystem
{
    private readonly IServerConnection _transport;
    private readonly ServerWorldService _worldService;
    private readonly ILogger<ServerSpawnSystem> _logger;

    public ServerSpawnSystem(
        in IServerConnection transport,
        in ServerWorldService worldService,
        in ILogger<ServerSpawnSystem> logger
    ) {
        _transport = transport;
        _worldService = worldService;
        _logger = logger;
    }

    public void Tick(float delta, DataStore store)
    {
        Result<SpawnRequest> receiveResult;
        while ((receiveResult = _transport.Receive<SpawnRequest>()).Success)
        {
            HandleSpawn(receiveResult.Value, store);
        }
    }

    private void HandleSpawn(SpawnRequest request, DataStore store)
    {
        string levelGuid = request.LevelGuid ?? string.Empty;

        bool levelLoaded = !string.IsNullOrEmpty(levelGuid) && _worldService.LoadLevel(levelGuid, store);

        Vector3 position;
        Quaternion orientation;
        if (levelLoaded && _worldService.TryGetSpawnPoint(levelGuid, request.CharacterId, store, out position, out orientation))
        {
            //  Restored per-character location (or the level spawn point).
        }
        else
        {
            position = levelLoaded ? _worldService.LevelSpawn : PlayerBodyConfig.DEFAULT_SPAWN_POSITION;
            orientation = Quaternion.Identity;
        }

        int entity = store.Alloc();
        Uuid uuid = store.GetUuid(entity);

        store.AddOrUpdate(entity, new NetworkComponent());
        store.AddOrUpdate(entity, new InputComponent());
        store.AddOrUpdate(entity, new TransformComponent(position, orientation, PlayerBodyConfig.PLAYER_SCALE));
        store.AddOrUpdate(entity, PlayerBodyConfig.CreatePhysics());
        store.AddOrUpdate(entity, PlayerBodyConfig.CreateCollider(PlayerBodyConfig.PLAYER_SCALE));

        _transport.Send(new SpawnResponse { Entity = uuid.ToValue(), Accepted = true });

        _logger.LogInformation("Spawned player entity {uuid} for character {character} in level {level}.", uuid, request.CharacterId, levelGuid);
    }
}