using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Server.Core.Components;
using WaywardBeyond.Server.Core.Saves;
using WaywardBeyond.Shared.Bricks;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Gameplay;
using WaywardBeyond.Shared.Networking.Components;
using WaywardBeyond.Shared.Networking.Sessions;
using WaywardBeyond.Shared.Networking.Transport;
using WaywardBeyond.Shared.Skills;

namespace WaywardBeyond.Server.Core.Systems;

/// <summary>
/// Server-authoritative join. Handles a client's <see cref="JoinRequest"/> by loading the level's
/// authoritative world, resolving the spawn (persisted per-character location or the level spawn),
/// allocating the player mirror and binding a session, then streaming the full world to that client - one
/// <see cref="WorldEntityAdd"/> per structure (bounded) followed by <see cref="WorldStreamComplete"/>, with
/// a <see cref="JoinAccept"/> carrying the level meta and server-assigned spawn. The client never authors
/// authoritative state; it builds view entities from the stream. Runs first in the world tick so
/// disconnect teardown and joins precede every other stage.
/// </summary>
public sealed class ServerJoinSystem : IServerWorldSystem
{
    private readonly ServerConnectionHub _hub;
    private readonly SessionManager _sessions;
    private readonly WorldSaveService _worldService;
    private readonly NetworkReplicationSystem _replication;
    private readonly ServerInteractionSystem _interaction;
    private readonly ServerJoinQueue _joinQueue;
    private readonly SkillDatabase? _skillDatabase;
    private readonly IBrickIdMap _brickIdMap;
    private readonly ILogger<ServerJoinSystem> _logger;

    private uint _nextSessionId;

    public ServerJoinSystem(
        in ServerConnectionHub hub,
        SessionManager sessions,
        in WorldSaveService worldService,
        in NetworkReplicationSystem replication,
        in ServerInteractionSystem interaction,
        in ILogger<ServerJoinSystem> logger,
        IBrickIdMap brickIdMap,
        in SkillDatabase? skillDatabase = null,
        in ServerJoinQueue? joinQueue = null
    ) {
        _hub = hub;
        _sessions = sessions;
        _worldService = worldService;
        _replication = replication;
        _interaction = interaction;
        _skillDatabase = skillDatabase;
        _brickIdMap = brickIdMap;
        _logger = logger;
        _joinQueue = joinQueue ?? new ServerJoinQueue();
    }

    public void Tick(float delta, DataStore store)
    {
        HandleDisconnects(store);

        //  Joins the world host pre-routed: it already bound the connection to this world's hub.
        foreach ((Uuid clientId, JoinRequest request) in _joinQueue.Drain())
        {
            HandleJoin(clientId, request, store);
        }

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
    /// Ends dropped client sessions: disposes the mirror's physics body (marshalled to the physics thread
    /// via its <see cref="PhysicsComponent.Dispose"/>) before freeing the entity so the subsequent publish
    /// stage replicates its despawn to remaining clients, then clears the session mappings.
    /// </summary>
    private void HandleDisconnects(DataStore store)
    {
        foreach (Uuid clientId in _hub.DrainDisconnects())
        {
            if (_sessions.TryGetEntity(clientId, out int entity))
            {
                if (store.TryGet(entity, out PhysicsComponent physics))
                {
                    physics.Dispose();
                }

                //  Capture the uuid before Free (which clears it) so the despawn can replicate.
                _replication.RequestDespawn(store.GetUuid(entity).ToValue());
                _interaction.ResetPlayerSequence(entity);
                store.Free(entity);
            }

            _sessions.EndSession(clientId);
            _logger.LogInformation("Ended session for client {client}.", clientId);

            //  Stamp the save's server-owned time played: a player's session ended abruptly.
            _worldService.EndSessionStamp();
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
            _interaction.ResetPlayerSequence(entity);
            store.Free(entity);
            _logger.LogInformation("Freed player mirror {entity} for client {client}.", entity, clientId);
        }

        _sessions.EndSession(clientId);

        //  Stamp the save's server-owned time played: this player's session ended.
        _worldService.EndSessionStamp();
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
            _interaction.ResetPlayerSequence(existing);
            store.Free(existing);
            _sessions.EndSession(clientId);
        }

        string levelGuid = request.LevelGuid ?? string.Empty;
        bool levelLoaded = !string.IsNullOrEmpty(levelGuid);
        if (levelLoaded)
        {
            try
            {
                levelLoaded = _worldService.LoadLevel(levelGuid, store);
            }
            catch (Exception ex)
            {
                //  A save hiccup never drops a join: play on the default spawn against the empty world.
                _logger.LogError(ex, "Failed to load level {level} for a joining client; continuing at the default spawn.", levelGuid);
                levelLoaded = false;
            }
        }

        Vector3 position;
        Quaternion orientation;
        if (levelLoaded)
        {
            try
            {
                bool restored = _worldService.TryGetSpawnPoint(levelGuid, request.CharacterId, store, out position, out orientation);
                if (!restored)
                {
                    //  Restored per-character location (or the level spawn point).
                    position = _worldService.LevelSpawn;
                    orientation = Quaternion.Identity;
                }
            }
            catch (Exception ex)
            {
                //  A save hiccup never drops a join: fall back to the level spawn.
                _logger.LogError(ex, "Failed to resolve the spawn for character {character} in level {level}; using the level spawn.", request.CharacterId, levelGuid);
                position = _worldService.LevelSpawn;
                orientation = Quaternion.Identity;
            }
        }
        else
        {
            position = PlayerBodyConfig.DEFAULT_SPAWN_POSITION;
            orientation = Quaternion.Identity;
        }

        //  Stamp the save's server-owned last-played: someone joined this save.
        if (levelLoaded)
        {
            _worldService.MarkActive();
        }

        int entity = store.Alloc();
        Uuid uuid = store.GetUuid(entity);
        _interaction.ResetPlayerSequence(entity);

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
        SeedSkillState(store, entity, request.Seed, _skillDatabase);

        Session session = new(_nextSessionId++);
        _sessions.Register(store, entity, clientId, session);

        //  The world stream is in flight: gate per-tick publishes until the complete is enqueued below
        //  (EndStream), so the client's Loading-time receive queue cannot pile up snapshots.
        _replication.BeginStream(clientId);

        //  Existing networked players (e.g. the host) were last published before this client connected,
        //  so their dirty flags are already consumed; request a one-shot full-state snapshot so they
        //  materialize as remote players on the joining client.
        _replication.RequestFullSync(clientId);

        Result accept = _hub.Send(clientId, new JoinAccept
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
        if (!accept.Success)
        {
            _logger.LogWarning("Failed to send join accept to client {clientId}: {message}.", clientId, accept.Message);
        }

        StreamWorld(clientId, store);
        Result stream = _hub.Send(clientId, new WorldStreamComplete { Dummy = 0 });
        if (!stream.Success)
        {
            _logger.LogWarning("Failed to send world stream complete to client {clientId}: {message}.", clientId, stream.Message);
        }
        _replication.EndStream(clientId);

        _logger.LogInformation("Joined player entity {uuid} for character {character} in level {level} on session {session}; streamed the world.", uuid, request.CharacterId, levelGuid, session.ID);
    }

    /// <summary>
    /// Seeds the server-authoritative interaction context from the joining client's local save. The
    /// client owns its initial save; once seeded these are server-owned and replicated to all clients.
    /// Starter inventory is a server-side grant: a client-authored new character seeds <c>null</c>
    /// contents and the server resolves the starter loadout, while a saved inventory is used as-is (and
    /// is never restocked). The inventory is normalized to the canonical slot count so the replicated
    /// array is always full-size and the active slot is always in bounds.
    /// </summary>
    private static void SeedInteractionContext(DataStore store, int entity, in CharacterSeed seed)
    {
        var inventory = new InventoryComponent();
        inventory.CopyFrom(PlayerInitialInventory.Resolve(seed.InventoryContents));
        store.AddOrUpdate(entity, inventory);
        store.AddOrUpdate(entity, new EquipmentComponent(InventoryComponent.ClampSlot(seed.ActiveInventorySlot)));
        store.AddOrUpdate(entity, new GameModeComponent((GameMode)seed.GameMode));
    }

    /// <summary>
    /// Seeds the player's transient, server-authoritative skill state from the joining client's saved
    /// statistics. The client owns the initial seed; only statistics whose id names a known skill are
    /// carried, and the whole component lives in memory for the session and is freed with the player
    /// mirror. The server never persists character skill data.
    /// </summary>
    private static void SeedSkillState(DataStore store, int entity, in CharacterSeed seed, in SkillDatabase? skillDatabase)
    {
        var xpBySkillId = new Dictionary<string, long>();
        if (skillDatabase != null && seed.Statistics != null)
        {
            foreach (Statistic statistic in seed.Statistics)
            {
                if (statistic.ID != null && skillDatabase.HasSkill(statistic.ID))
                {
                    xpBySkillId[statistic.ID] = statistic.Value;
                }
            }
        }

        store.AddOrUpdate(entity, new SkillStateComponent(xpBySkillId));
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

            //  Attach the brick palette so the client can resolve the server's registry ids locally.
            Result send = _hub.Send(clientId, new WorldEntityAdd { VoxelEntity = VoxelEntityDataCodec.EncodeToPalette(data, _brickIdMap) });
            if (!send.Success)
            {
                _logger.LogWarning("Failed to stream world entity to client {clientId}: {message}.", clientId, send.Message);
            }
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
