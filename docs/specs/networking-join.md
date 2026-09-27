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

2. **`ServerJoinSystem`** (`Server.Core/Systems/`) — via `WorldSaveService`
   (`Server.Core/Saves/`) — loads the authoritative voxel world for `LevelGuid`
   from the `levels` bucket (unloading any previous world, disposing its physics
   bodies), resolves the spawn transform (the persisted `<level>.character.<id>`
   location, else `Level.Spawn`), allocates the server mirror, seeds the
   server's interaction context from `CharacterSeed`, binds it to a fresh
   `Session`, and replies `JoinAccept { Level, SpawnTransform, PlayerEntity }`.

3. It then streams the world as one `WorldEntityAdd { VoxelEntityData }` per
   structure (bounded per-entity), followed by a `WorldStreamComplete`. The
   client builds a view entity for each arrival (mesh + a local prediction
   collider) on the ECS thread, and **only** transitions to `Playing` on
   `WorldStreamComplete`.

## `Playing` as the gate

`Playing` keeps `ClientReconcileSystem` inert until the world is fully streamed.
One-time transport queues do not preserve cross-type order (no envelope), so the
client defers `WorldSnapshot` application until `WorldStreamComplete` arrives.
Subsequent per-tick replication keeps using the `WorldSnapshot` path; streaming
is a join-time event, not ongoing AOI.

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
`WorldsClient` whose responses the ECS thread completes.

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