# Networking — Join Handshake and World Streaming

One subject: how a client joins a server, and how the server streams the world.

## Model

The server owns the `levels` KV bucket, world generation, and per-character
location persistence. Clients own only `characters` and no longer generate or
save worlds. World data is streamed to the client during join.

## Join flow

1. **`ClientJoinSystem`** (`Client.Core/Systems/`) submits a
   `JoinRequest { LevelGuid, CharacterId, PublicView, CharacterSeed }`.
   - `PublicView` is the minimal identity relay (`CharacterId`, `Name`, `Body`)
     used for remote rendering.
- `CharacterSeed` is a separate optional field carrying the client's
      authoritative **initial** interaction context (inventory, equipment/active
      slot, game mode) from its local save. It is **non-nullable**; an older
      client omitting it receives the struct default (empty/creative), which is
      the desired backward-compat behavior.
   - `CharacterSeed.Statistics` carries the client's saved skill XP; the server
      keeps only statistics that name a known skill (see
      [skills](skills.md)).
   - It drives `MainMenu → Loading → Playing` and is queued from the menu so
      view building runs on the client ECS thread.

2. **World routing** — `ServerWorldHost` (`Server.Core/ServerWorldHost.cs`)
   consumes the client's `JoinRequest` from the pending set, creates (or finds)
   the world for `LevelGuid` with a fresh per-world graph, binds the connection
   to that world's hub, and queues the join into the world. `ServerJoinSystem`
   (`Server.Core/Systems/`) then — via the world's `WorldSaveService`
   (`Server.Core/Saves/`) — loads the authoritative voxel world for `LevelGuid`
   from the `levels` bucket into that world's store, resolves the spawn
   transform (the persisted `<level>.character.<id>` location, else
   `Level.Spawn`; a save hiccup falls back to the level spawn), allocates the
   server mirror, seeds the server's interaction context from `CharacterSeed`,
   binds it to a fresh `Session`, and replies `JoinAccept { Level, SpawnTransform,
   PlayerEntity }`. Worlds are isolated per level; see
   [networking-worlds](networking-worlds.md).

3. It then streams the world as one `WorldEntityAdd { VoxelEntityData }` per
   structure (bounded per-entity), followed by a `WorldStreamComplete`. Each
   entity carries a brick palette so the client can resolve the server's
   registry ids locally (`ClientJoinSystem`, see
   [brick-identity](brick-identity.md)). The
   client builds a view entity for each arrival (mesh + a local prediction
   collider) on the ECS thread, and **only** transitions to `Playing` on
   `WorldStreamComplete`.

## Save switch and rejoin

A player who returns to the menu sends `LeaveGameRequest`. The server ends the
session and frees the player mirror. The host then returns the connection to
the pending set (`ServerWorldHost.ReturnSessionlessConnectionsToPending`, `ServerWorldHost.cs:89`), so
the player's next `JoinRequest` is world-routed (see
[networking-worlds](networking-worlds.md)).

A world serves exactly one level. A join that names another level is refused
with an error log; the host creates a separate world for that level instead.
This keeps the first world and its players intact when a second player switches
saves.

## `Playing` as the gate

`Playing` keeps `ClientReconcileSystem` inert until the world is fully streamed.
One-time transport queues do not preserve cross-type order (no envelope), so the
client defers `WorldSnapshot` application until `WorldStreamComplete` arrives.
Subsequent per-tick replication keeps using the `WorldSnapshot` path; streaming
is a join-time event, not ongoing AOI. The full-sync request rides the same
join tick: `ServerJoinSystem` requests the one-shot full-state snapshot in the
same tick that it streams the world, so the full-state publish lands alongside
the first deltas, all after the stream completes.

A remote disconnect during join or loading is handled the same as one during
play: `ClientDisconnectSystem` drops the dead transport and returns the client
to the menu with a connection-lost toast. The character save is gated on
`Playing`, so a mid-join disconnect simply abandons the stream. The teardown
also faults every in-flight world-management operation (`WorldsClient`), so the
save screen and world list never await a vanished server, and it runs for any
menu state - a server death on the save screen returns the client to the main
menu. A join attempt with no active connection fails fast through the same
teardown instead of entering Loading.

## Join-stream timeout

A join whose `WorldStreamComplete` never arrives (undelivered marker, dying
link, or a world too large to drain) must not stall `Loading` forever.
`ClientJoinSystem` starts a clock when the `JoinRequest` is sent; if the stream
does not complete within `NetworkingSettings.JoinStreamTimeoutMs`, it aborts the
join and asks `ClientDisconnectSystem` for the normal connection-lost teardown:
menu, toast, transport down. The server bounds the other side of the stream:
a client whose reliable send backlog stays over
`ReliableQueueDisconnectThreshold` (2x the
`ReliableQueueConcernThreshold` logging threshold by default) for
`ReliableQueueDisconnectMs` is disconnected (see
[transports](networking-transports.md)).

## Character ownership nuance

- The client's local `characters` bucket remains the client-owned **storage**
  (the source of the join-time seed).
- Interaction-relevant inventory counts and game mode are **server-owned after
  join** and replicated downstream.
- The **active slot** stays **client-owned** (authoritative on
  `EquipmentComponent`) and replicates upstream for the server to validate against.
- Skill XP is seeded from the client's statistics at join into a transient
  server-side `SkillStateComponent`, then server-owned for the session. See
  [skills](skills.md).

## Save-listing menu

The save-listing menu is served by the server: `NewWorldRequest`,
`ListWorldsRequest`, `DeleteWorldRequest`, and `SaveWorldRequest` (flush
authoritative world) map to `WorldSaveService` operations, driven by a client
`WorldsClient` whose responses the ECS thread completes. Menu-time operations
(create/list/delete) are served by `ServerWorldManager`
(`Server.Core/ServerWorldManager.cs`) against connections that have not joined
a world yet; the in-world `SaveWorldRequest` is served by the world's
`ServerWorldSystem`.

### Continue and the multiplayer page

- **Continue** branches on `ProfileSettings.LastServerMode` (`profile.toml`):
  a `Remote` marker (written on every multiplayer connect) reconnects to the
  persisted endpoint on the save page before the save list and join flow target
  that server; a `Local` marker (written when a host-mode session loads) uses
  the in-process server. The endpoint itself is the last-used
  `NetworkingSettings.DefaultHost`/`DefaultConnectPort` (see
  [config-schemas](config-schemas.md)).
- **Saved servers** (the multiplayer page's saved list, capped at 32, deduped
  by host:port) connect through the standard `ConnectRemote` + join flow and
  update the persisted endpoint and `LastServerMode` like any other connect.
- **Keyboard submit**: pressing Enter in the address or port field triggers the
  same connect path as the connect button (including validation).

The old `ClientPlayerSpawnSystem`/`ServerSpawnSystem` spawn path and client load
stages were removed in favor of join.

## Source of truth

- `WaywardBeyond.Client.Core/Systems/ClientJoinSystem.cs`
- `WaywardBeyond.Server.Core/Systems/ServerJoinSystem.cs`
- `WaywardBeyond.Server.Core/Saves/WorldSaveService.cs`
- `WaywardBeyond.Shared.Data/CodeGen/world.nsd` (the join/stream messages)

## Tests that pin this

- `Swordfish.Tests` codec/seed tests for `CharacterSeed`.
- `WaywardBeyond.Client.Core.Tests` join/stream client behavior.