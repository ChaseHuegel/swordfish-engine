using System;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NsdRpc;
using Krill;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Networking;
using WaywardBeyond.Gameplay.Statistics;
using WaywardBeyond.Items;
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
    SimulationStep simulationStep,
    ILogger<ServerJoinSystem> logger,
    GameplayConfig gameplayConfig,
    ISkillDatabase skillDatabase,
    ServerJoinQueue joinQueue,
    ServerPeers peers,
    IUserPermissionService permissions,
    ISlotGroupSchemaDatabase? schemas = null,
    IItemDatabase? itemDatabase = null
) : IServerWorldSystem
{
    private uint _nextSessionId;

    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        //  One malformed entry must never unwind the world tick: every join, leave, and teardown is
        //  isolated so the remaining clients keep simulating and the queue keeps draining.
        HandleDisconnects(store);

        //  Leaves run before joins so a player who returns to the menu and rejoins in the same tick
        //  has the old session torn down first.
        while (joinQueue.TryDequeueLeave(out Uuid clientId))
        {
            try
            {
                HandleLeave(clientId, store);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to end the session for client {client}.", clientId);
            }
        }

        //  Joins the world host pre-routed: it already bound the peer to this world.
        while (joinQueue.TryDequeueJoin(out ServerJoinQueue.JoinCall join))
        {
            try
            {
                HandleJoin(join, store);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to seat client {client}.", join.Peer.GameId);
                join.Completion.TrySetResult(RpcResult<JoinAccept>.Failure(
                    GameRpcErrors.JOIN_REFUSED, "the player could not be seated"));
            }
        }
    }

    /// <summary>
    /// Ends dropped client sessions: frees the mirror entity so the engine releases its physics body
    /// before the subsequent publish stage replicates its despawn to remaining clients, then clears
    /// the session mappings.
    /// </summary>
    private void HandleDisconnects(DataStore store)
    {
        while (joinQueue.TryDequeueDisconnect(out Uuid clientId))
        {
            try
            {
                HandleDisconnect(clientId, store);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to tear down the session for client {client}.", clientId);
            }
        }
    }

    private void HandleDisconnect(Uuid clientId, DataStore store)
    {
        permissions.Unbind(clientId);
        replication.CancelStream(clientId);

        if (sessions.TryGetEntity(clientId, out int entity))
        {
            ReleasePlayerMirror(entity, store);
        }

        if (sessions.EndSession(clientId))
        {
            logger.LogInformation("Ended session for client {client}.", clientId);

            //  Stamp the save's server-owned time played: a player's session ended abruptly.
            saveService.EndSessionStamp();
        }
    }

    /// <summary>
    /// Ends a player's server session when they return to the menu: saves the character location,
    /// disposes and frees the player mirror, publishes its despawn to remaining clients, and clears
    /// the session mapping. The connection is deliberately left registered on the hub so the client
    /// can join again.
    /// </summary>
    private void HandleLeave(Uuid clientId, DataStore store)
    {
        replication.CancelStream(clientId);

        if (sessions.TryGetEntity(clientId, out int entity))
        {
            ReleasePlayerMirror(entity, store);
            logger.LogInformation("Freed player mirror {entity} for client {client}.", entity, clientId);
        }

        if (sessions.EndSession(clientId))
        {
            //  Stamp the save's server-owned time played: this player's session ended.
            saveService.EndSessionStamp();
        }

        permissions.Unbind(clientId);
    }

    private void ReleasePlayerMirror(int entity, DataStore store)
    {
        saveService.QueueSaveCharacterLocation(store, entity);

        //  Free captures every component removal; the engine releases the physics body.
        //  Capture the uuid before Free (which clears it) so the despawn can replicate.
        replication.RequestDespawn(store.GetUuid(entity).ToValue());
        store.Free(entity);
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

        //  The character uuid is client-authored and becomes the mirror's entity identity. A zero or
        //  already-owned uuid would be rejected by the store, so validate it before any work is done.
        if (request.CharacterUuid == 0UL)
        {
            completion.TrySetResult(RpcResult<JoinAccept>.Failure(GameRpcErrors.JOIN_REFUSED, "the character id is invalid"));
            return;
        }

        Uuid uuid = Uuid.FromValue(request.CharacterUuid);

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
            replication.CancelStream(clientId);
            ReleasePlayerMirror(existing, store);
            sessions.EndSession(clientId);
        }

        //  The mirror is allocated with the character's uuid, so the uuid must belong to this character
        //  alone: refuse another player's mirror or a level structure instead of throwing from the store.
        if (store.TryGet(uuid, out _))
        {
            logger.LogWarning(
                "Refusing join for client {clientId}: character {uuid} is already owned by another entity.",
                clientId, uuid);
            completion.TrySetResult(RpcResult<JoinAccept>.Failure(GameRpcErrors.JOIN_REFUSED, "the character id is in use"));
            return;
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
        GameMode gameMode;
        if (levelLoaded)
        {
            GameMode defaultMode = saveService.CurrentLevel?.DefaultGameMode ?? request.State.GameMode;
            try
            {
                Result<CharacterEntityData> savedCharacter = saveService.GetCharacterEntity(request.CharacterUuid);
                if (savedCharacter.Success)
                {
                    CharacterEntityData saved = savedCharacter.Value;
                    position = new Vector3((float)saved.X, (float)saved.Y, (float)saved.Z);
                    orientation = new Quaternion(saved.OrientationX, saved.OrientationY, saved.OrientationZ, saved.OrientationW);
                    gameMode = ResolveGameMode(saved.GameMode);
                }
                else
                {
                    position = saveService.LevelSpawn;
                    orientation = Quaternion.Identity;
                    gameMode = ResolveGameMode(defaultMode);
                }
            }
            catch (Exception ex)
            {
                //  A save hiccup never drops a join: fall back to the level spawn and default mode.
                logger.LogError(ex, "Failed to resolve the spawn for character {character} in level {level}; using the level spawn.", request.CharacterUuid, levelGuid);
                position = saveService.LevelSpawn;
                orientation = Quaternion.Identity;
                gameMode = ResolveGameMode(defaultMode);
            }
        }
        else
        {
            position = gameplayConfig.DefaultSpawnPosition;
            orientation = Quaternion.Identity;
            gameMode = ResolveGameMode(request.State.GameMode);
        }

        //  Stamp the save's server-owned last-played: someone joined this save.
        if (levelLoaded)
        {
            saveService.MarkActive();
        }

        //  A same-character rejoin reuses the mirror uuid, so drop the previous session's look totals
        //  before the first command. The client resets its totals to zero at seat.
        simulationStep.ForgetLookHistory(uuid);

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
        store.AddOrUpdate(entity, new IdentifierComponent(CharacterNames.Clamp(request.View.Name), tag: "game"));

        //  Seed the server-authoritative interaction context from the joining client's local save and
        //  the level's resolved game mode. From here the server owns these components and replicates them.
        SeedInteractionContext(store, entity, request.State, gameMode);
        CharacterStatisticsSeeder.SeedSkillState(store, entity, request.State.Statistics, skillDatabase);
        CharacterStatisticsSeeder.SeedStatistics(store, entity, request.State.Statistics, skillDatabase);

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
    /// contents and the server resolves the starter loadout for the resolved game mode, while a saved
    /// inventory is used as-is (and is never restocked). Stack limits are clamped to authoritative item
    /// definitions and the active slot is clamped to the hotbar capacity.
    /// </summary>
    private void SeedInteractionContext(DataStore store, int entity, in CharacterState seed, GameMode gameMode)
    {
        var inventory = InventoryComponent.CreatePlayer(schemas);
        KeyedSlotEntry[] initial = PlayerInitialInventory.Resolve(seed.KeyedInventory, seed.Inventory, gameMode);
        if (itemDatabase != null && initial.Length > 0)
        {
            initial = ClampMaxStackSizes(initial, itemDatabase);
        }

        inventory.LoadSparse(initial);
        store.AddOrUpdate(entity, inventory);
        store.AddOrUpdate(entity, new EquipmentComponent(inventory.ClampHotbarSlot(seed.ActiveInventorySlot)));
        store.AddOrUpdate(entity, new GameModeComponent(gameMode));
    }

    private static KeyedSlotEntry[] ClampMaxStackSizes(KeyedSlotEntry[] entries, IItemDatabase itemDatabase)
    {
        var clamped = new KeyedSlotEntry[entries.Length];
        for (var i = 0; i < entries.Length; i++)
        {
            KeyedSlotEntry entry = entries[i];
            if (entry.Item.IsEmpty())
            {
                clamped[i] = entry;
                continue;
            }

            Result<Item> itemDef = itemDatabase.Get(entry.Item.ID);
            if (!itemDef.Success || itemDef.Value.MaxStack <= 0)
            {
                clamped[i] = entry;
                continue;
            }

            int maxStack = itemDef.Value.MaxStack;
            int count = Math.Clamp(entry.Item.Count, 1, maxStack);
            clamped[i] = new KeyedSlotEntry(
                entry.GroupId,
                entry.LocalIndex,
                entry.Item with { Count = count, MaxSize = maxStack });
        }

        return clamped;
    }

    /// <summary>
    /// Resolves the seeded game mode. An undefined wire value falls back to adventure, which applies
    /// survival rules, so a malformed save cannot grant creative placement.
    /// </summary>
    private static GameMode ResolveGameMode(GameMode gameMode)
    {
        return Enum.IsDefined(gameMode) ? gameMode : GameMode.Adventure;
    }
}
