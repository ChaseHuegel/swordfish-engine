using System.Numerics;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Server.Core.Saves;
using WaywardBeyond.Shared.Gameplay;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Components;
using WaywardBeyond.Shared.Networking.Sessions;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Server.Core.Systems;

/// <summary>
/// Server-authoritative spawn. Handles each client's <see cref="SpawnRequest"/> by allocating a player
/// entity on the server world, constructing its physics body (server owns body construction), wiring it
/// for replication, binding it to a session, and replying to that client with the assigned uuid. The
/// server assigns the initial transform, which replicates downstream; the client never authors
/// authoritative state. Each request is routed back to the connection it arrived on via the hub.
/// </summary>
public sealed class ServerSpawnSystem : IEntitySystem
{
    private readonly ServerConnectionHub _hub;
    private readonly SessionManager _sessions;
    private readonly ServerWorldService _worldService;
    private readonly ILogger<ServerSpawnSystem> _logger;

    private uint _nextSessionId;

    public ServerSpawnSystem(
        in ServerConnectionHub hub,
        SessionManager sessions,
        in ServerWorldService worldService,
        in ILogger<ServerSpawnSystem> logger
    ) {
        _hub = hub;
        _sessions = sessions;
        _worldService = worldService;
        _logger = logger;
    }

    public void Tick(float delta, DataStore store)
    {
        foreach ((Uuid clientId, SpawnRequest request) in _hub.Receive<SpawnRequest>())
        {
            HandleSpawn(clientId, request, store);
        }
    }

    private void HandleSpawn(Uuid clientId, SpawnRequest request, DataStore store)
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

        Session session = new(_nextSessionId++);
        _sessions.Register(store, entity, clientId, session);

        _hub.Send(clientId, new SpawnResponse { Entity = uuid.ToValue(), Accepted = true });

        _logger.LogInformation("Spawned player entity {uuid} for character {character} in level {level} on session {session}.", uuid, request.CharacterId, levelGuid, session.ID);
    }
}