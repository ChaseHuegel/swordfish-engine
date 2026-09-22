using System.Collections.Generic;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using WaywardBeyond.Server.Core.Components;
using WaywardBeyond.Server.Core.Saves;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Gameplay;
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
    private readonly NetworkReplicationSystem _replication;
    private readonly ILogger<ServerJoinSystem> _logger;

    private uint _nextSessionId;

    public ServerJoinSystem(
        in ServerConnectionHub hub,
        SessionManager sessions,
        in WorldSaveService worldService,
        in NetworkReplicationSystem replication,
        in ILogger<ServerJoinSystem> logger
    ) {
        _hub = hub;
        _sessions = sessions;
        _worldService = worldService;
        _replication = replication;
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
    /// Ends a player's server session when they return to the menu: disposes and frees the player mirror,
    /// publishes its despawn to remaining clients, and clears the session mapping. The connection is
    /// deliberately left registered on the hub so the client can join again.
    /// </summary>
    private void HandleLeave(Uuid clientId, DataStore store)
    {
        if (_sessions.TryGetEntity(clientId, out int entity))
        {
            if (store.TryGet(entity, out PhysicsComponent physics))
            {
                physics.Dispose();
            }

            //  Capture the uuid before Free (which clears it) so the despawn can replicate.
            _replication.RequestDespawn(store.GetUuid(entity).ToValue());
            store.Free(entity);
            _logger.LogInformation("Freed player mirror {entity} for client {client}.", entity, clientId);
        }

        _sessions.EndSession(clientId);
    }

    private void HandleJoin(Uuid clientId, JoinRequest request, DataStore store)
    {
        if (_sessions.TryGetEntity(clientId, out int existing))
        {
            if (store.TryGet(existing, out PhysicsComponent physics))
            {
                physics.Dispose();
            }

            //  Capture the uuid before Free (which clears it) so the old mirror despawns to peers.
            _replication.RequestDespawn(store.GetUuid(existing).ToValue());
            store.Free(existing);
            _sessions.EndSession(clientId);
        }

        string levelGuid = request.LevelGuid ?? string.Empty;
        bool levelLoaded = !string.IsNullOrEmpty(levelGuid) && _worldService.LoadLevel(levelGuid, store);

        if (levelLoaded && _worldService.TryGetSpawnPoint(levelGuid, request.CharacterId, store, out Vector3 position, out Quaternion orientation))
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
        //  are not relayed for remote rendering.
        store.AddOrUpdate(entity, new BodyViewComponent { Body = request.PublicView.Body });
        store.AddOrUpdate(entity, new IdentifierComponent(request.PublicView.Name, tag: PlayerBodyConfig.PLAYER_TAG));

        //  Seed the server-authoritative interaction context from the joining client's local save. The
        //  client owns its initial save; from here the server owns these components and replicates them.
        SeedInteractionContext(store, entity, request.Seed);

        Session session = new(_nextSessionId++);
        _sessions.Register(store, entity, clientId, session);

        //  Existing networked players (e.g. the host) were last published before this client connected,
        //  so their dirty flags are already consumed; request a one-shot full-state snapshot so they
        //  materialize as remote players on the joining client.
        _replication.RequestFullSync(clientId);

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

    /// <summary>
    /// Seeds the server-authoritative interaction context (inventory, active slot, game mode) from the
    /// joining client's local save. The client owns its initial save; once seeded these are server-owned
    /// and replicated to all clients. An older client omitting the seed yields the struct default, which
    /// is a sane (empty/creative) context.
    /// </summary>
    private static void SeedInteractionContext(DataStore store, int entity, in CharacterSeed seed)
    {
        store.AddOrUpdate(entity, new InventoryComponent(seed.InventoryContents ?? []));
        store.AddOrUpdate(entity, new EquipmentComponent(seed.ActiveInventorySlot));
        store.AddOrUpdate(entity, new GameModeComponent((GameMode)seed.GameMode));
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
