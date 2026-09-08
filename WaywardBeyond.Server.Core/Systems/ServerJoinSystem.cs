using System.Collections.Generic;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using WaywardBeyond.Server.Core.Components;
using WaywardBeyond.Server.Core.Saves;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Gameplay;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Components;
using WaywardBeyond.Shared.Networking.Sessions;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Server.Core.Systems;

/// <summary>
/// Server-authoritative join. Handles a client's <see cref="JoinRequest"/> by loading the level's
/// authoritative world, resolving the spawn (persisted per-character location or the level spawn),
/// allocating the player mirror and binding a session, then streaming the full world to that client - one
/// <see cref="WorldEntityAdd"/> per structure (bounded) followed by <see cref="WorldStreamComplete"/>, with
/// a <see cref="JoinAccept"/> carrying the level meta and server-assigned spawn. The client never authors
/// authoritative state; it builds view entities from the stream.
/// </summary>
public sealed class ServerJoinSystem : IEntitySystem
{
    private readonly ServerConnectionHub _hub;
    private readonly SessionManager _sessions;
    private readonly WorldSaveService _worldService;
    private readonly ILogger<ServerJoinSystem> _logger;

    private uint _nextSessionId;

    public ServerJoinSystem(
        in ServerConnectionHub hub,
        SessionManager sessions,
        in WorldSaveService worldService,
        in ILogger<ServerJoinSystem> logger
    ) {
        _hub = hub;
        _sessions = sessions;
        _worldService = worldService;
        _logger = logger;
    }

    public void Tick(float delta, DataStore store)
    {
        foreach ((Uuid clientId, JoinRequest request) in _hub.Receive<JoinRequest>())
        {
            HandleJoin(clientId, request, store);
        }

        foreach ((Uuid clientId, LeaveGameRequest _) in _hub.Receive<LeaveGameRequest>())
        {
            HandleLeave(clientId, store);
        }
    }

    /// <summary>
    /// Ends a player's server session when they return to the menu: disposes and frees the player mirror
    /// and clears the session mapping. The connection is deliberately left registered on the hub so the
    /// client can join again. No despawn is broadcast (single-player N=1; a LAN follow-up can route it).
    /// </summary>
    private void HandleLeave(Uuid clientId, DataStore store)
    {
        if (_sessions.TryGetEntity(clientId, out int entity))
        {
            if (store.TryGet(entity, out PhysicsComponent physics))
            {
                physics.Dispose();
            }

            store.Free(entity);
            _logger.LogInformation("Freed player mirror {entity} for client {client}.", entity, clientId);
        }

        _sessions.EndSession(clientId);
    }

    private void HandleJoin(Uuid clientId, JoinRequest request, DataStore store)
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
        store.AddOrUpdate(entity, new OwnedCharacterComponent(request.CharacterId));

        //  Relay the joining client's minimal public character view: the appearance index is replicated
        //  so remote clients can materialize this player, and the name rides on the reused
        //  IdentifierComponent (mirroring the client's local player). Inventory/attributes/statistics
        //  never leave the client.
        store.AddOrUpdate(entity, new BodyViewComponent { Body = request.PublicView.Body });
        store.AddOrUpdate(entity, new IdentifierComponent(request.PublicView.Name, tag: PlayerBodyConfig.PLAYER_TAG));

        Session session = new(_nextSessionId++);
        _sessions.Register(store, entity, clientId, session);

        _hub.Send(clientId, new JoinAccept
        {
            Level = _worldService.CurrentLevel ?? new Level(),
            SpawnX = position.X,
            SpawnY = position.Y,
            SpawnZ = position.Z,
            OrientationX = orientation.X,
            OrientationY = orientation.Y,
            OrientationZ = orientation.Z,
            OrientationW = orientation.W,
            PlayerEntity = uuid.ToValue(),
        });

        StreamWorld(clientId, store);
        _hub.Send(clientId, new WorldStreamComplete { Dummy = 0 });

        _logger.LogInformation("Joined player entity {uuid} for character {character} in level {level} on session {session}; streamed the world.", uuid, request.CharacterId, levelGuid, session.ID);
    }

    private void StreamWorld(Uuid clientId, DataStore store)
    {
        CollectWorldEntitiesAction action = new();
        store.Query<VoxelEntityDataComponent, NetworkComponent, TransformComponent, CollectWorldEntitiesAction>(0f, ref action);

        foreach (int entity in action.Entities)
        {
            VoxelEntityData data = VoxelWorldEntityFactory.ToVoxelEntityData(store, entity);
            if (data.Uuid == 0)
            {
                continue;
            }

            _hub.Send(clientId, new WorldEntityAdd { VoxelEntity = data });
        }
    }

    private struct CollectWorldEntitiesAction : IForEach<VoxelEntityDataComponent, NetworkComponent, TransformComponent>
    {
        public readonly List<int> Entities;

        public CollectWorldEntitiesAction()
        {
            Entities = new List<int>();
        }

        public void Execute(float delta, DataStore store, int entity, in VoxelEntityDataComponent content, in NetworkComponent network, in TransformComponent transform)
        {
            Entities.Add(entity);
        }
    }
}