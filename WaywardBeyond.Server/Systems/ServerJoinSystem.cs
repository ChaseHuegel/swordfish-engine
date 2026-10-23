using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NsdRpc;
using Swordfish.ECS;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Networking;
using WaywardBeyond.Networking.Rpc;
using WaywardBeyond.Server.Components;
using WaywardBeyond.Server.Permissions;
using WaywardBeyond.Server.Saves;
using WaywardBeyond.Skills;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// Server-authoritative join. Handles a client's <see cref="JoinRequest"/> by loading the level's
/// authoritative world, resolving the spawn (persisted per-character location or the level spawn),
/// allocating the player mirror and binding a session, then streaming the full world to that client - one
/// <see cref="LevelEntityAdd"/> per structure (bounded) followed by <see cref="LevelStreamComplete"/>, with
/// a <see cref="JoinAccept"/> carrying the level meta and server-assigned spawn. The client never authors
/// authoritative state; it builds view entities from the stream. Runs first in the world tick so
/// disconnect teardown and joins precede every other stage.
/// </summary>
public sealed class ServerJoinSystem(
    SessionManager sessions,
    LevelSaveService saveService,
    NetworkReplicationSystem replication,
    ILogger<ServerJoinSystem> logger,
    GameplayConfig gameplayConfig,
    ISkillDatabase skillDatabase,
    ServerJoinQueue joinQueue,
    ServerPeers peers,
    IUserPermissionService permissions
) : IServerWorldSystem
{
    private uint _nextSessionId;

    public void Tick(float delta, DataStore store)
    {
        HandleDisconnects(store);

        //  Leaves run before joins so a player who returns to the menu and rejoins in the same tick
        //  has the old session torn down first.
        foreach (Uuid clientId in joinQueue.DrainLeaves())
        {
            HandleLeave(clientId, store);
        }

        //  Joins the world host pre-routed: it already bound the peer to this world.
        foreach (ServerJoinQueue.JoinCall join in joinQueue.Drain())
        {
            HandleJoin(join, store);
        }
    }

    /// <summary>
    /// Ends dropped client sessions: disposes the mirror's physics body (marshalled to the physics thread
    /// via its <see cref="PhysicsComponent.Dispose"/>) before freeing the entity so the subsequent publish
    /// stage replicates its despawn to remaining clients, then clears the session mappings.
    /// </summary>
    private void HandleDisconnects(DataStore store)
    {
        foreach (Uuid clientId in joinQueue.DrainDisconnects())
        {
            permissions.Unbind(clientId);

            if (sessions.TryGetEntity(clientId, out int entity))
            {
                if (store.TryGet(entity, out PhysicsComponent physics))
                {
                    physics.Dispose();
                }

                //  Capture the uuid before Free (which clears it) so the despawn can replicate.
                replication.RequestDespawn(store.GetUuid(entity).ToValue());
                store.Free(entity);
            }

            if (sessions.EndSession(clientId))
            {
                logger.LogInformation("Ended session for client {client}.", clientId);

                //  Stamp the save's server-owned time played: a player's session ended abruptly.
                saveService.EndSessionStamp();
            }
        }
    }

    /// <summary>
    /// Ends a player's server session when they return to the menu: disposes and frees the player mirror,
    /// publishes its despawn to remaining clients, and clears the session mapping. The connection is
    /// deliberately left registered on the hub so the client can join again.
    /// </summary>
    private void HandleLeave(Uuid clientId, DataStore store)
    {
        if (sessions.TryGetEntity(clientId, out int entity))
        {
            if (store.TryGet(entity, out PhysicsComponent physics))
            {
                physics.Dispose();
            }

            //  Capture the uuid before Free (which clears it) so the despawn can replicate.
            replication.RequestDespawn(store.GetUuid(entity).ToValue());
            store.Free(entity);
            logger.LogInformation("Freed player mirror {entity} for client {client}.", entity, clientId);
        }

        if (sessions.EndSession(clientId))
        {
            //  Stamp the save's server-owned time played: this player's session ended.
            saveService.EndSessionStamp();
        }

        permissions.Unbind(clientId);
    }

    private void HandleJoin(in ServerJoinQueue.JoinCall join, DataStore store)
    {
        ServerPeer peer = join.Peer;
        Uuid clientId = peer.GameId;
        JoinRequest request = join.Request;
        TaskCompletionSource<RpcResult<JoinAccept>> completion = join.Completion;

        if (join.CancellationToken.IsCancellationRequested)
        {
            completion.TrySetResult(RpcResult<JoinAccept>.Failure(GameRpcErrors.INVALID_STATE, "join cancelled"));
            return;
        }

        if (!peers.TryGet(clientId, out _))
        {
            completion.TrySetResult(RpcResult<JoinAccept>.Failure(GameRpcErrors.NOT_JOINED, "the peer disconnected"));
            return;
        }

        string levelGuid = request.LevelGuid ?? string.Empty;

        //  A world serves exactly one level. A join naming another level means routing failed:
        //  never switch the live world, which would free every other player's mirror.
        if (!string.IsNullOrEmpty(levelGuid)
            && saveService.CurrentLevelGuid != null
            && saveService.CurrentLevelGuid != levelGuid)
        {
            logger.LogError(
                "Refusing join for client {clientId}: world is level {current} but the join names {level}.",
                clientId, saveService.CurrentLevelGuid, levelGuid);
            completion.TrySetResult(RpcResult<JoinAccept>.Failure(GameRpcErrors.JOIN_REFUSED, "the world serves another level"));
            return;
        }

        if (sessions.TryGetEntity(clientId, out int existing))
        {
            if (store.TryGet(existing, out PhysicsComponent physics))
            {
                physics.Dispose();
            }

            //  Capture the uuid before Free (which clears it) so the old mirror despawns to peers.
            replication.RequestDespawn(store.GetUuid(existing).ToValue());
            store.Free(existing);
            sessions.EndSession(clientId);
        }

        bool levelLoaded = !string.IsNullOrEmpty(levelGuid);
        if (levelLoaded)
        {
            try
            {
                levelLoaded = saveService.LoadLevel(levelGuid, store);
            }
            catch (Exception ex)
            {
                //  A save hiccup never drops a join: play on the default spawn against the empty world.
                logger.LogError(ex, "Failed to load level {level} for a joining client; continuing at the default spawn.", levelGuid);
                levelLoaded = false;
            }
        }

        Vector3 position;
        Quaternion orientation;
        if (levelLoaded)
        {
            try
            {
                bool restored = saveService.TryGetSpawnPoint(request.CharacterUuid, out position, out orientation);
                if (!restored)
                {
                    //  Restored per-character location (or the level spawn point).
                    position = saveService.LevelSpawn;
                    orientation = Quaternion.Identity;
                }
            }
            catch (Exception ex)
            {
                //  A save hiccup never drops a join: fall back to the level spawn.
                logger.LogError(ex, "Failed to resolve the spawn for character {character} in level {level}; using the level spawn.", request.CharacterUuid, levelGuid);
                position = saveService.LevelSpawn;
                orientation = Quaternion.Identity;
            }
        }
        else
        {
            position = gameplayConfig.DefaultSpawnPosition;
            orientation = Quaternion.Identity;
        }

        //  Stamp the save's server-owned last-played: someone joined this save.
        if (levelLoaded)
        {
            saveService.MarkActive();
        }

        Uuid uuid = Uuid.FromValue(request.CharacterUuid);
        int entity = store.Alloc(uuid);

        store.AddOrUpdate(entity, NetworkComponent.Create());
        store.AddOrUpdate(entity, new InputComponent());
        store.AddOrUpdate(entity, new TransformComponent(position, orientation, CharacterDefaults.PlayerScale));
        store.AddOrUpdate(entity, CharacterDefaults.CreateCharacterPhysicsComponent());
        store.AddOrUpdate(entity, CharacterDefaults.CreateCharacterColliderComponent(CharacterDefaults.PlayerScale));
        store.AddOrUpdate(entity, new OwnedCharacterComponent());

        //  Relay the joining client's minimal public character view: the appearance index is replicated
        //  so remote clients can materialize this player, and the name rides on the reused
        //  IdentifierComponent (mirroring the client's local player). Inventory/attributes/statistics
        //  are not relayed for remote rendering. The "game" tag is the client teardown key: it lets the
        //  remote client free the mirror with the rest of its gameplay entities on exit.
        store.AddOrUpdate(entity, new BodyViewComponent { Body = request.View.Body });
        store.AddOrUpdate(entity, new IdentifierComponent(request.View.Name, tag: "game"));

        //  Seed the server-authoritative interaction context from the joining client's local save. The
        //  client owns its initial save; from here the server owns these components and replicates them.
        SeedInteractionContext(store, entity, request.State);
        SeedSkillState(store, entity, request.State, skillDatabase);

        Session session = new(_nextSessionId++);
        sessions.Register(store, entity, clientId, session);

        //  Bind the joining peer's claim so permission checks resolve against this session. The
        //  claim arrives once in ClientHello; a peer with no hello joins anonymous.
        permissions.Bind(clientId, peer.Claim, peer.IsLocal);

        //  The world stream is in flight: gate per-tick publishes until the complete is enqueued below
        //  (EndStream), so the client's Loading-time receive queue cannot pile up snapshots.
        replication.BeginStream(clientId);

        //  Existing networked players (e.g. the host) were last published before this client connected,
        //  so their dirty flags are already consumed; request a one-shot full-state snapshot so they
        //  materialize as remote players on the joining client.
        replication.RequestFullSync(clientId);

        join.Completion.TrySetResult(RpcResult<JoinAccept>.Success(new JoinAccept
        {
            Level = saveService.CurrentLevel ?? new Level(),
            SpawnX = position.X,
            SpawnY = position.Y,
            SpawnZ = position.Z,
            OrientationX = orientation.X,
            OrientationY = orientation.Y,
            OrientationZ = orientation.Z,
            OrientationW = orientation.W,
            PlayerEntity = uuid.ToValue(),
        }));

        //  The stream is a separate call. The gate lifts when the stream call completes.
        logger.LogInformation("Joined player entity {uuid} for character {character} in level {level} on session {session}.", uuid, request.CharacterUuid, levelGuid, session.ID);
    }

    /// <summary>
    /// Seeds the server-authoritative interaction context from the joining client's local save. The
    /// client owns its initial save; once seeded these are server-owned and replicated to all clients.
    /// Starter inventory is a server-side grant: a client-authored new character seeds <c>null</c>
    /// contents and the server resolves the starter loadout, while a saved inventory is used as-is (and
    /// is never restocked). The inventory is normalized to the canonical slot count so the replicated
    /// array is always full-size and the active slot is always in bounds.
    /// </summary>
    private static void SeedInteractionContext(DataStore store, int entity, in CharacterState seed)
    {
        var inventory = new InventoryComponent();
        inventory.CopyFrom(PlayerInitialInventory.Resolve(seed.Inventory));
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
    private static void SeedSkillState(DataStore store, int entity, in CharacterState seed, ISkillDatabase skillDatabase)
    {
        var xpBySkillId = new Dictionary<string, long>();
        if (seed.Statistics != null)
        {
            foreach (Statistic statistic in seed.Statistics)
            {
                if (statistic.ID != null && skillDatabase.Get(statistic.ID).Success)
                {
                    xpBySkillId[statistic.ID] = statistic.Value;
                }
            }
        }

        store.AddOrUpdate(entity, new SkillStateComponent(xpBySkillId));
    }

}
