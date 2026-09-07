# Phase 4 — State ownership & join streaming (implementation plan)

Goal: server owns the `levels` bucket, world-gen, the authoritative voxel world, and per-character
location persistence. Client owns only `characters`. World data streams to the client during join.

**Decisions locked with the user:**
- DTO position precision **stays `double`** (do NOT switch `VoxelEntityData`/`CharacterEntityData` to float).
- Save **listing/delete becomes an async server query** (`ListWorldsRequest/Response`).
- Work through the whole phase, **one build-green commit per milestone**, then review each in post.

All work is `[S]`/`[G]`; **no engine (`[E]`) changes are required.** Commit boundaries respect the
`LOCAL-SERVER-SINGLEPLAYER.md` rules (engine-first / standalone / shared code).

---

## M0 — Baseline + DTO consolidation groundwork (4.6, `[S]`)

No `.nsd` field-type changes (keep `double`). Focus: one shared brick-id source, remove duplicated
`FNV1a`, and confirm streamed == persisted payload shapes usable server-side.

- Move `FNV1a` → `WaywardBeyond.Shared.Data/FNV1a.cs` (`public`, add `ComputeDataID(string) → ushort`
  using `ComputeHash32 % ushort.MaxValue`).
- Re-point `Client.Core/Bricks/BrickDatabase.cs` (`GenerateDataID`) to the shared `FNV1a`; delete
  `Client.Core/Serialization/FNV1a.cs`.

Commit: `Move brick id hashing into shared data` (game/shared, build-green).

---

## M1 — Shared voxel-generation substrate (4.1 part, `[S]`)

Add to `WaywardBeyond.Shared.Gameplay` (net9.0; add `LibNoise 0.2.0` package):
- `Voxels/VoxelChunkWriter.cs` — shared chunked voxel buffer producing `ChunkInfo[]`. Port of the
  `VoxelObject` chunk-store `Set`/`GetChunkInfos` logic (identical chunk offsets/layout, so colliders
  and the client mesh build are byte-for-byte consistent). Keyed on `(short, short, short)` (no client
  `Short3` dep). Constructs codegen `Chunk(byte Size, Voxel[])` / `ChunkInfo(short X, short Y, short Z, Chunk)`.
- `Bricks/WorldMaterialCatalog.cs` — `WorldBricks`: `Rock/Ice/Core` voxels via
  `FNV1a.ComputeDataID(name)` + block shape + 0 light ⇒ `Voxel(id, 0, 0)` (matches `BrickInfo.ToVoxel()`
  for `any`→`Block`/`block` materials; `ShapeLight` byte = block(0) | 0<<4 = 0).
- `Generation/Noise/SimplexPerlinExtensions.cs` — moved from client (`GetLayeredNoiseValue`).
- `Generation/AsteroidGenerator.cs` — moved + refactored: writes into a `VoxelChunkWriter`, returns a
  `GeneratedVoxelEntity` (Uuid, Position, Orientation, `ChunkInfo[]`) instead of calling the mesh-coupled
  `VoxelEntityBuilder.Create`.
- `Generation/WorldGenerator.cs` — moved + refactored: produces `GeneratedVoxelEntity[]` (20 asteroids)
  purely from seed; **no ECS/entity building, no meshing**.
- `Generation/GeneratedVoxelEntity.cs` — readonly struct of the generated output.

Consumer surface for later milestones: server persists these as `VoxelEntityData` and builds authority
entities via existing `VoxelWorldEntityFactory.CreateAuthority`.

Client `WorldGenerator`/`AsteroidGenerator`/`WorldGenNewGameStage`/`StarterShipNewGameStage` stay in
place (build-green); they are removed in M2/M3 once the server new-world path lands.

**Test** (`Swordfish.Tests/SharedWorldGenTests.cs`, headless, in M1 so the substrate is proven):
- `WorldMaterialCatalog` produces distinct nonzero `rock`/`ice`/`core` ids and the expected voxels.
- `WorldGenerator.Generate(seed)` returns 20 `GeneratedVoxelEntity`, each whose `VoxelColliderBuilder`
  produces a non-empty `CompoundShape` (collidable) and at least one solid voxel.
- Determinism: same seed twice ⇒ identical per-entity chunk voxel counts + transforms.
- `VoxelChunkWriter` parity: writing the same voxels as the client writer yields the same `ChunkInfo[]`
  layout (offsets).

Commit: `Add shared voxel world generation` (shared; build-green).

---

## M2 — Server `WorldSaveService` owns `levels` + new-world path (4.1/4.2, `[G/S]`)

### nsd (`Shared.Networking/CodeGen/network.nsd`)
- `NewWorldRequest { string Name = 0; int Seed = 1; GameMode default = 2 }`
- `NewWorldResponse { string LevelGuid = 1; bool Success = 2 }`
- `ListWorldsRequest {}` / `ListWorldsResponse { Level[] Levels = 50 }`
- `DeleteWorldRequest { string LevelGuid = 0 }` / `DeleteWorldResponse { bool Success = 1 }`
  (`GameMode` already exists in `Shared.Data`.)

### `Server.Core/Saves/WorldSaveService.cs` (replaces interim `ServerWorldService`)
Owns `levels` KV:
- `CreateWorld(name, seed)`: build `Level` (Guid, hashed seed, spawn, `GameMode`), run shared
  `WorldGenerator` off-thread to `GeneratedVoxelEntity[]`, persist `Level` meta + per-entity
  `VoxelEntityData`; return guid. Does **not** build authority entities (those arise on join via lookup).
- `LoadLevel(guid, store)`: unloads previous world (disposing physics bodies) and builds authority
  entities from KV — reuse existing logic from `ServerWorldService` (now reading our own writes).
- `SaveLocation(guid, characterId, VoxelEntityData)`: sample server-authoritative transform on
  interval/disconnect/shutdown.
- `ListLevels()`, `DeleteLevel(guid)`.
- Autosave on the server's own schedule; **flush on server stop** awaited before `PersistentNatsProcess`
  dispose (explicit sequencing point; client stop → server flush → NATS dispose).

### Server systems
- New `ServerWorldSystem` (or extend `ServerSpawnSystem`) handles `NewWorldRequest`/`ListWorldsRequest`/
`DeleteWorldRequest` → routes replies to the requesting client.
- `ServerContext` teardown invokes `WorldSaveService.Flush()`.

### Client contraction (4.2/4.5)
- `GameSaveService` shrinks: character save stays (`CharacterSaveManager`); save **listing/delete/create**
  become server queries via a new client `WorldsClient` that sends the requests and awaits responses
  (menu calls `GetSaves()` become async; `SelectSavePage`/`NewSavePage`/`HomePage` callers adapted).
- `GameSaveManager` keyboard/window triggers delegate world-saving to the server; retains character save.

Commit the server save service first (build-green), then the client listing/contract in a second commit.

---

## M3 — Join handshake + full-world stream (4.3/4.4, `[G]`)

### nsd
- `JoinRequest { string LevelGuid = 0; ulong CharacterId = 1; PublicView PublicView = 2 }`
- `PublicView { ulong CharacterId = 0; string Name = 1; int Body = 2 }`
- `JoinAccept { Level Level = 0; float SpawnX/Y/Z = 1..3; Quat SpawnOrientation = 4..7;
   ulong PlayerEntity = 8 }`
- `WorldEntityAdd { VoxelEntityData VoxelEntity = 0 }`
- `WorldStreamComplete {}`

### Server
- `ServerJoinSystem` replaces `ServerSpawnSystem`: on `JoinRequest`, load/build the world from KV
  (`WorldSaveService`), resolve spawn (`<guid>.character.<id>` or `Level.Spawn`), allocate + reply
  `JoinAccept`, then stream each `WorldEntityAdd` (per entity, bounded) + `WorldStreamComplete`, routed
  per client. Builds the player mirror + `NetworkComponent`. Session binding per existing phase-3 hub.
- Optionally hold `WorldSnapshot` publishing until that client's stream completes (per-client gate).

### Client
- `ClientJoinSystem` drives `GameState: MainMenu → Joining → Playing`, replacing
  `GameSaveService.Load`/`RequestSpawn`. Sends `JoinRequest`; drains `JoinAccept`, then `WorldEntityAdd`
  messages building **view entities on the client ECS thread** (`VoxelEntityViewBuilder`: build a client
  `VoxelObject` from `ChunkInfo[]`, reusing existing `VoxelEntityBuilder` internals for mesh + local
  colliders via shared `VoxelColliderBuilder`); seats player at server-assigned spawn; `WorldStreamComplete`
  flips to Playing.
- Gate `ClientReconcileSystem` on `GameState.Playing && streamComplete` (replaces the loading-thread race
  guard rationale).
- `PlayerCharacterEntityBuilder.Decorate` reused to materialize the player.
- Remove `ClientPlayerSpawnSystem`, `ServerSpawnSystem`, `CharacterEntityLoadStage`/`VoxelEntityLoadStage`
  `RequestSpawn` usage; `SelectCharacterPage` load now invokes `ClientJoinSystem`.

Commit server join, then client join/view-builder, then delete the old spawn/load path.

---

## M4 — Character ownership validation + public view (4.5, `[G]`)

- Confirm no inventory/attributes/statistics cross the wire; `characters` bucket client-owned unchanged.
- `JoinRequest.PublicView` carries only Id/Name/Body (remote-visual relay stays out of scope / deferred
  as the plan's 2.2 defers remote rendering; roster minimal for N=1).
- `SelectSavePage` lists worlds via server query (done in M2/M3); character menus stay local.
- `SelectCharacterPage` load triggers join.

---

## M5 — Tests (6.2) + docs (6.1) + checklist

Headless `Swordfish.Tests` (Linux CI friendly, `LocalConnection` + bare `DataStore` + fixed-step physics):
- Join/stream: server streams `WorldEntityAdd` → client allocates view entities → player seated at
  restored location; reconcile remains inert until `WorldStreamComplete`.
- Server `WorldSaveService` persistence round-trip (generate → persist → reload → authority bodies).
- New-world determinism + collider parity (from M1).
- Save listing/delete over the request/response path (stubbed KV or real local NATS if the bundled binary
  runs on Linux).
- World-gen worldgen no longer runs client-side (older client paths deleted).

Docs: update `NETWORKING.md`, `LOCAL-SERVER-SINGLEPLAYER.md` (check the 4.x boxes, risk register), and
`AGENTS.md` where save/join/stream architecture changed.

---

## Commit order (each build-green) & status

- [ ] M0 shared FNV1a + client re-point  — *game/shared*
- [ ] M1 shared worldgen substrate + tests — *shared*
- [ ] M2a server `WorldSaveService` + nsd — *game*
- [ ] M2b client world save contraction + server listing — *game*
- [ ] M3a server join/stream handler — *game*
- [ ] M3b client join system + view builder — *game*
- [ ] M3c delete old spawn/load path — *game*
- [ ] M4 character ownership/public view — *game*
- [ ] M5 tests + docs + checklist — *game*

## Risks / notes
- Brick id parity: shared `WorldMaterialCatalog` recomputes `FNV1a` ids; client `BrickDatabase` keeps its
  own order-dependent collision pass. Worldgen materials (`rock`/`ice`/`core`) are collinear with the
  client algorithm (collision probability negligible). Kept a single `FNV1a` implementation in shared.
- Menu listing becomes async via ECS-thread transport → menu callers await a `TaskCompletionSource`
  completed by a client system; concurrency confined to the request/response service.
- Shutdown flush must be awaited before `PersistentNatsProcess.Dispose` (explicit sequencing point).
- World-switch must dispose server physics bodies (reuse phase-3 disconnect path).