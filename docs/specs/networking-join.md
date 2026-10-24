# Networking — Join Handshake and World Streaming

One subject: how a client joins a server and how the server streams the world.
The service catalogue lives in [networking-rpc](networking-rpc.md).

## Model

The server owns the per-level save databases, generation, and per-character
location persistence. Clients own only `profile.db` and no longer generate or
save levels. Level data is streamed to the client during join.

## Join flow

0. **Connection hello.** `ClientRpcHost` connects the loopback at startup, or a
   remote TCP endpoint through `ConnectRemote`, then sends
   `ClientHello { UserId }` from `profile.toml`. `ServerHandlers` stores the
   claim on `ServerPeer`. Menu calls resolve permissions from that claim.
   See [permissions](permissions.md).

1. **`JoinAsync`.** `ClientJoinSystem` calls
   `JoinRequest { LevelGuid, CharacterUuid, CharacterView, CharacterState }`.
   - `CharacterView` is the minimal identity relay (`CharacterUuid`, `Name`,
     `Body`) used for remote rendering.
   - `CharacterState` carries the client's authoritative **initial** interaction
     context (inventory, active slot, game mode) and its saved skill statistics.
     It is non-nullable. An older client that omits it receives the struct
     default, which is the desired compatibility behavior.
   - `ServerHandlers` enqueues the call. `ServerHostCommands` runs on the server
     thread, creates or finds the world for `LevelGuid`, binds the peer, and
     hands the join to the world's `ServerJoinSystem`.

2. **Seat the player.** `ServerJoinSystem` loads the authoritative voxel world
   for `LevelGuid` through the world's `LevelSaveService`, resolves the spawn
   (persisted per-character location, else `Level.Spawn`; a save hiccup falls
   back to the level spawn), allocates the server mirror with the character's
   uuid, seeds the interaction context and skill state from `CharacterState`,
   binds a fresh `Session`, and binds the peer's claim to the session's
   permission service. The local peer gets the all-granting set.
   `JoinAsync` returns `JoinAccept { Level, SpawnTransform, PlayerEntity }`
   only after the session and mirror exist.

3. **`StreamLevelAsync`.** `ClientJoinSystem` starts the stream call with
   `ClientInbound` as the `IChunkSink<LevelStreamChunk>`. The server thread
   encodes each level entity into a serialized `VoxelEntityData` payload, splits
   it into `LevelStreamChunk` fragments, and sends them under the
   `LevelStreamBytesPerTick` budget. It checks `RpcPeer.Stats.QueuedBytes` and
   `RpcContext.TryStream`, and aborts on a rejected send. The final result is
   `RpcResult<LevelStreamComplete>`.

4. **Build the view.** The client ECS tick drains `ClientInbound.LevelChunks`,
   reassembles each entity with `FragmentAssembler`, and builds a view entity
   (mesh plus a local prediction collider). A rejected part fails the join. It
   enters `Playing` only after the stream response lands and every chunk
   applied.

## Worlds and routing

`ServerWorldHost` owns the world table. A join names a level. The host creates
the world and loads it before the join is processed, so a join can never unload
under itself. A world serves exactly one level. A join that names another level
is refused with `JoinRefused`; the host creates a separate world instead. See
[networking-worlds](networking-worlds.md).

A player who returns to the menu sends `LeaveGame`. The world tick ends the
session and frees the player mirror. The host clears the peer's world binding,
so the next `JoinAsync` routes to the world it names. A peer can switch saves
without reconnecting.

## `Playing` as the gate

`Playing` keeps `ClientReconcileSystem` inert until the world is fully streamed.
The server opens a snapshot gate at join (`BeginStream`) and lifts it after the
stream response (`EndStream`). The one-shot full-sync snapshot is exempt and
stays the client's first snapshot.

The client coalesces any queued snapshot burst on entering `Playing`: the newest
frame applies fully, and each superseded frame still applies its non-motion
components first, so one-shot server state cannot be lost.

## Disconnect and reconnect

A remote drop during join or loading runs the same teardown as a drop during
play. `ClientDisconnectSystem` saves the character while still in `Playing`,
requests cleanup, returns to the menu with a toast, and calls
`ClientRpcHost.Disconnect`. The host restores a fresh loopback session, so the
save list and a local join work without an application restart. See
[transports](networking-transports.md#client-host).

The teardown frees every entity tagged `"game"`: the local player, remote
player mirrors, and world geometry. `RpcClient` faults every pending call on
disconnect, so no menu request or join waits forever.

## Join-stream timeout

`ClientJoinSystem` starts a `CancellationTokenSource` with
`Protocol.JoinStreamTimeoutMs` when the join begins. The token covers both
`JoinAsync` and `StreamLevelAsync`. A timeout or a failed result cancels the
calls and runs the normal disconnect teardown.

The server bounds the other side with the same key. The RPC call deadline resets
on every stream chunk, and a peer that cannot drain its send backlog is closed
with `BacklogLimit` (see [rpc](networking-rpc.md#backlog-and-idle-policy)). A
stream that exceeds `Protocol.JoinStreamTimeoutMs` aborts and closes the peer.
A client that opens the gate at join and never starts a stream is reaped on the
same deadline. Leaving or disconnecting clears the gate immediately, so an
abandoned join cannot leak a gated session.

## Character ownership nuance

- The client's local `profile.db` is the client-owned storage and the source of
  the join-time seed.
- Interaction-relevant inventory counts and game mode are server-owned after
  join and replicate downstream.
- The active slot stays client-owned (`EquipmentComponent`) and replicates
  upstream for the server to validate.
- Skill XP is seeded from the client's statistics at join into a transient
  server-side `SkillStateComponent`, then server-owned for the session. See
  [skills](skills.md).

## Save-listing menu

The save-listing menu is served by the server. `ListLevels`, `CreateLevel`,
`DeleteLevel`, and the in-world `SaveLevel` are RPC calls. `LevelsClient` is a
thin async wrapper over `GameServerApiClient`; it has no FIFO queue or waiter
table. `ServerLevelManager` serves menu calls on the server thread, so
`ILevelCatalog` access stays single-threaded. `ServerSaveSystem` serves the
in-world save call in the world tick. See [persistence](persistence.md) and
[permissions](permissions.md).

### Continue and the multiplayer page

- **Continue** branches on `ProfileSettings.LastServerMode`. A `Remote` marker
  reconnects to the persisted endpoint before the save list and join target that
  server. A `Local` marker uses the restored loopback.
- **Saved servers** are capped at 32 and deduped by host:port. They use the same
  `ConnectRemote` and join path.
- **Keyboard submit** in the address or port field uses the same connect path as
  the button.

## Source of truth

- `WaywardBeyond.Client/Networking/ClientRpcHost.cs`
- `WaywardBeyond.Client/Systems/ClientJoinSystem.cs`
- `WaywardBeyond.Client/Saves/{GameSaveManager,GameSaveService}.cs`
- `WaywardBeyond.Server/ServerHostCommands.cs`
- `WaywardBeyond.Server/Systems/ServerJoinSystem.cs`
- `WaywardBeyond.Server/ServerPeers.cs`
- `WaywardBeyond.Data/Nsd/levels.nsd`
- `WaywardBeyond.Data/Nsd/network.nsd`
