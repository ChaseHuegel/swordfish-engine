# LOCAL-SERVER-SINGLEPLAYER

Development plan and tracker for making the singleplayer experience run on an authoritative local
server, with the multiplayer architecture this enables.

Status legend: `[ ]` pending · `[~]` in progress · `[x]` done

---

## Purpose

Ship singleplayer as **the multiplayer architecture** running a server in the same process. This
eliminates a class of SP-vs-MP drift bugs, simplifies testing (one environment), avoids maintaining
divergent code paths, and keeps "open to LAN"/transfer-to-dedicated-server available later.

The singleplayer server **is** the multiplayer server. There will never be a separate singleplayer
simulation path.

## Locked decisions

1. **In-process local server** for singleplayer + future LAN hosting. A dedicated executable stays a
   *clean seam only* — **no `WaywardBeyond.Server.Launcher` this initiative**.
2. **Server-authoritative simulation with client prediction** for all players, including the local one.
3. **Kinematic look.** The client sends the sensitivity-resolved **absolute look orientation** (a
   gimbal-free quaternion, accumulated from cursor + Q/E roll) and the server applies it directly.
   **No client camera smoothing**; no torque-driven look (torque look was always temporary).
   The local player's orientation stays client-predicted (reconcile seats it once at spawn and then only
   corrects position/velocity), so the authoritative echo never snaps the view.
4. **Mouse sensitivity is a client-local setting** — never networked, never a shared/duplicated
   constant. The wire carries *resolved* look, not config.
5. **Fixed-step physics with tick-tagged commands.** `JoltPhysicsSystem` already integrates the solver
   at a fixed `0.016s` internal step with its own accumulator and a per-step `FixedUpdate` event. Input
   is applied **once per physics step** via that event, using **tick-tagged commands** (each
   `InputComponent` carries its target server tick; multiple samples for the same tick collapse to the
   newest). Both sides apply the identical command-per-step mapping, which is what makes "same input
   sequence → same state" hold across two independently-paced threads. (See 1.4.)
6. **Canonical sim tick.** One clock drives the tick-tag system: **sim tick = physics-step ordinal**.
   `TickNumber`, `LastProcessedInput`, `ServerTickAtSample`, and `LastAckedInput`/`LastAckedSnapshot`
   are all rebased onto that ordinal. The server's per-world replication tick is **not** the tag clock;
   snapshots publish `TickNumber` = sim tick at publish.
7. **Terrain authority is phased, not single-step.** Phase 1 is **arena-only** (no terrain colliders on
   the server). Phase 2 adds server-side voxel colliders built from save data. LAN/streaming must not
   ship ahead of Phase 2.
8. **Full-world stream on join.** The server streams the complete Level meta + all voxel world data at
   join, sent **per entity** (`WorldEntityAdd { VoxelEntityData }` each + `WorldStreamComplete`) rather
   than one unbounded batch. Chunked/AOI streaming is explicitly deferred.
9. **Clients keep local colliders for prediction.** Client-side world entities keep their own physics
   colliders so the local player can predict against terrain; the server stays authoritative and
   reconcile corrects. Dual-physics cost accepted.
10. **LAN sessions before world streaming.** The join path is designed against N-connected clients from
    the start (Phase 3) before the world-streaming phase (Phase 4).
11. **State ownership split.** Clients own `characters` (identity, attributes, statistics, inventory).
    Servers own the `levels` bucket: Level meta, voxel entities, world generation, and per-character
    location persistence. **Clients stop owning world generation** after Phase 4; world data is streamed
    at join.
12. **No equipment persistence; `Character` schema is complete as-is** (`Body` + inventory). The only
    character data transmitted is a **minimal public view** (Id, Name, Body) for remote-player visuals;
    inventory/attributes/statistics **never** leave the client.
13. **Remote-player visuals are Phase 2 scope** (relaying the public character view), landing with the
    server voxel world.
14. **No UDP/reliability/envelope layer.** No `GamePacket`, `FrameStream`, `IDataSender`/
    `IDataReceiver`, or packet-level sequence/ack. TCP for any socket transport. The gameplay
    `SequenceNumber`/`LastAckedInput`/`LastAckedSnapshot` fields are prediction acks and are kept.
15. **Engine-change policy.** Engine code is anything **except `WaywardBeyond.*`** (`Swordfish`,
    `Swordfish.ECS`, `Swordfish.Library`, `Swordfish.Integrations`, `Swordfish.Compilation`, `Shoal`,
    `Reef`, `Shoal.Extensions.Swordfish`, launchers, demo/editor). **Any engine changes commit
    separately from game changes — never intertwined** — and engine changes commit **first**, standalone
    and building green, so game commits build on the new engine API. Engine changes are a **last
    resort**: a task first tries game/shared-side implementation; an engine change is acceptable only if
    it is generic/reusable, and **game-specific behavior is never implemented in the engine**. Each
    `[E]` change must state its justification and be separable for independent cherry-pick/merge into
    the engine project.
16. **World-body dynamics are Dynamic and replicated.** World/voxel bodies stay **Dynamic** (as today,
    `VoxelEntityBuilder.cs:66`) and are **server-authored**: structure motion (thruster-driven forces,
    collision impulses) is simulated by the authoritative server and replicated per tick via the
    existing `TransformComponent`/`PhysicsComponent` snapshot path. Clients snap authoritative structure
    state each tick (drift corrected like the player's); `ThrusterSystem` and structural force systems
    move into the **shared deterministic step** on both worlds. Voxel **content** mutation (editing
    blocks) remains out of scope.

## Architecture invariants

- Client world and server world are separate `World`s/`DataStore`s on separate threads, speaking only
  serialized nsd messages through `INetworkTransport`.
- **No shared-`DataStore` / zero-copy "it's local" shortcuts — ever.** Even the singleplayer loopback
  must serialize through the wire format (`LocalConnection` already does).
- All validation/clamps that affect simulation live in **shared code** so prediction and server clamp
  identically. Nothing server-only that the client prediction replays divergently.
- The shared simulation step runs **per physics step** (driven from `JoltPhysicsSystem.FixedUpdate`,
  not the ECS tick) and applies **tick-tagged commands**; it never uses wall-clock `delta` for
  integration math.
- The gameplay simulation step is deterministic: same input sequence → same state, same binary.

## Working rules — commit boundaries & engine policy

- **Engine set**: everything except `WaywardBeyond.*` (`Swordfish`, `Swordfish.ECS`,
  `Swordfish.Library`, `Swordfish.Integrations`, `Swordfish.Compilation`, `Shoal`, `Reef`,
  `Shoal.Extensions.Swordfish`, launchers, demo/editor). **Game set**: `WaywardBeyond.*`.
- **Task labels**: every task below is tagged `[E]` (engine), `[S]` (shared/game-helper), or `[G]`
  (game).
- **Commit rule**: an `[E]` change is its own engine commit, committed **before** the game commits that
  consume it, and must build standalone. A mixed engine+game commit is never allowed.
- **Last-resort rule**: game tasks must not hack around the engine, and must not request engine
  changes without a generic justification; game-specific needs stay in game code.
- **Engine change register**: see the table after the risk register — one row per `[E]` change with
  justification and why it must be engine, not game.

## Persistence substrate

All save data flows through a **local NATS JetStream server** (`PersistentNatsProcess` launches
`nats-server -js -sd "saves/"`), wrapped by `KeyValueStore` (NATS KV) into buckets. Today the client
writes both buckets; the plan moves one of them server-side.

| Bucket | Key pattern | Payload | Owner (target) |
|---|---|---|---|
| `characters` | `<characterId>` | `Character` (Id, Name, attributes, Body, Statistics, Inventory) | **Client** (unchanged) |
| `levels` | `<level.Guid>` | `Level` meta (Version, Seed, spawn, GameMode, Name) | **Server** |
| `levels` | `<guid>.entity.<uuid>` | `VoxelEntityData` (chunked voxels + transform) | **Server** |
| `levels` | `<guid>.character.<characterId>` | per-character location in world (position/orientation/GameMode) | **Server** |

Shared DTOs live in `WaywardBeyond.Shared.Data/CodeGen/{saves,voxels}.nsd` (`Character`, `Level`,
`VoxelEntityData`/`VoxelEntityData.ChunkInfo.Chunk.Voxel`, `CharacterEntityData`). No sqlite, no loose
files (except a legacy disk-migration path retained in `GameSaveService`).

## Non-goals (explicitly out of scope)

- Dedicated-server launcher executable (`WaywardBeyond.Server.Launcher`).
- UDP / reliability / ordering / RTT layer; any envelope in `NETWORKING.md` (none remains).
- AOI interest management / chunked streaming (join is a full-world batch).
- Voxel **content** mutation replication (editing blocks across clients) and mob/AI gameplay
  propagation — the deeper "world-sim initiative" (structure **motion** replication is in scope; only
  static world data is out of it).
- Anti-cheat beyond shared-step input clamping.
- Replicating character inventory/attributes/statistics in any form.
- Client-side world generation after Phase 4.

---

## Current-state snapshot (what the plan builds on)

### Networking wiring
- Two worlds: client `ECSContext` (`Swordfish/ECS/ECSContext.cs`) on `"ECS"` thread; server
  `ServerContext` (`Server.Core/ServerContext.cs`) on `"Server"` thread.
- `ServerContext` adds only `NetworkReplicationSystem` + `ServerSpawnSystem` (`ServerContext.cs:31-32`).
  **No simulation.**
- Server hosted in-process: `Client.Core/Injector.cs:143` → `ServerComposition.Register(container)`
  (`Server.Core/ServerComposition.cs:10`) → `RegisterMany<ServerContext>`. `Server.Core` has no
  `manifest.toml`.
- Networking registrations (`Client.Core/Injector.cs:121-144`):
  - `NetworkRegistry.Initialize([InputComponent assembly])`
  - `TransformComponent` uuid 2 / `PhysicsComponent` uuid 3, both ServerOwned, explicit codecs
  - nsd serializers for `WorldSnapshot`, `SpawnRequest`, `SpawnResponse`
  - `LocalConnection` singleton; `IClientConnection` → `.Client`, `IServerConnection` → `.Server`
  - systems: `ClientInputSystem`, `ClientReplicationSystem`, `ClientReconcileSystem`,
    `ClientPlayerSpawnSystem`; then `ServerComposition.Register(container)`.
- `ServerPlayerOwnership` (`Server.Core/ServerPlayerOwnership.cs`) — single `Uuid?`, used to skip
  echoing the owner's transform (`NetworkReplicationSystem.cs:181-186`).
- `SessionManager` (`Server.Core/SessionManager.cs`) — session↔entity maps; **registered but not
  wired** (`Injector.cs:134`).
- `TcpTransport` (`Shared.Networking/Transport/TcpTransport.cs`) — single shared receive queue
  (`:19`), no per-type demux; never exercised.

### Message/component shapes
- `Shared.Networking/CodeGen/network.nsd`: `ComponentSnapshot`, `WorldSnapshot`, `SpawnRequest`,
  `SpawnResponse`, `TransformMessage`, `PhysicsMessage`.
- `Shared.Networking/CodeGen/components.nsd`: `InputComponent`
  (`MovementX/Y/Z`, `LookDeltaX/Y` = raw cursor px, `Jump`, `SequenceNumber`, `ServerTickAtSample`).
- `NetworkComponent` (`Shared.Networking/Components/`): `Session`, `LastAckedInput`,
  `LastAckedSnapshot`, `ServerTPS`.
- `PendingInputComponent`: 256-entry ring buffer (`Push`, `AckUpTo`, `GetPending`).

### Persistence (current)
- `GameSaveService` (`Client.Core/Saves/`) — owns `levels` KV ops: `GetSaves`, `CreateSave`
  (builds `Level` meta), `Load` (stages + world-gen-if-empty + legacy disk migration), `Save`
  (queries client ECS for `VoxelComponent`/`CharacterComponent` entities), `Delete`.
- `GameSaveManager` — active save, autosave timer, F5 quicksave, save-on-close/pause triggers,
  `SaveAndExit`, ECS cleanup.
- `CharacterSaveManager` + `NatsCharacterStorage` (`characters` bucket) — client-owned `Character`
  identity/inventory; `GameSaveService.Save` also writes per-character world location under `levels`.
- Load stages: `CharacterEntityLoadStage` (read `<guid>.character.<id>` or spawn point →
  `ClientPlayerSpawnSystem.RequestSpawn`), `VoxelEntityLoadStage` (read `<guid>.entity.*` →
  `VoxelEntityBuilder.Create`), `CharacterLoadStage`, plus new-game stages
  `WorldGenNewGameStage` (`WorldGenerator`) and `StarterShipNewGameStage` — all client-side.
- `PersistentNatsProcess` started from client `Entry.cs:74`; `KeyValueStore` registered at
  `Injector.cs:94`.

### Current end-to-end (singleplayer)
`ClientPlayerSpawnSystem.RequestSpawn` → `SpawnRequest` → server `ServerSpawnSystem` allocates mirror
+ `NetworkComponent`, `ServerPlayerOwnership.SetOwnedPlayer`, replies `SpawnResponse` → client
allocates entity at same uuid, decorates, sends initial `TransformComponent` placement → server
`ApplyPlacement` seats+clears dirty. Per frame: `ClientInputSystem` samples → `PendingInputComponent`
→ `ClientReplicationSystem` sends dirty `InputComponent` up → server applies to mirror and advances
acks. `PlayerControllerSystem` (client-only) drives local motion — client-authoritative. Server
publishes no authoritative state for the owned player; reconcile is inert.

---

## Phase 1 — Arena authority (server-authoritative player sim)

### 1.1 [G] Redefine networked look input
`[x]` Change `InputComponent` from raw cursor deltas to sensitivity-resolved absolute angles.
- Edit `Shared.Networking/CodeGen/components.nsd`: replace `LookDeltaX/LookDeltaY` with
  `LookYaw`/`LookPitch` (radians, absolute). Regenerate via `nsdc -r -p -i CodeGen -o ./CodeGen/Output`
  (project builds run `RunCodeGen` automatically).
- **Acceptance:** `InputComponent` compiles with `LookYaw`/`LookPitch`; no nsd/code reference to
  `LookDeltaX/Y` remains.

### 1.2 [G] Client sampling applies sensitivity locally
`[x]` Update `Client.Core/Systems/ClientInputSystem.cs`:
- Accumulate `_inputService.CursorDelta` across ticks; maintain running yaw/pitch state.
- Fold `MOUSE_SENSITIVITY` + `ControlSettings.LookSensitivity` into the resolved radian output at
  sample time. Sensitivity is now permanently a **client-local** concern.
- Keep `SequenceNumber`/`ServerTickAtSample`, `PendingInputComponent` push, and write to the entity
  unchanged.
- **Acceptance:** sampled `InputComponent` carries absolute yaw/pitch. No shared code path reads
  sensitivity.

### 1.3 [G] Canonicalize `PhysicsMessage` payload semantics
`[x]` `PhysicsComponent.Torque` is dual-semantics today: accumulated torque pre-step, but
`body.GetAngularVelocity()` after Jolt sync (`JoltPhysicsSystem.cs:254,307`). Resolve it:
- Add `AngularVelocity` (X/Y/Z) and drop/relabel `Torque` in `network.nsd` `PhysicsMessage`; update
  `PhysicsCodec` (`Client.Core/Networking/PhysicsCodec.cs`) to the canonical fields. The component
  value is only well-defined at sync boundaries — document it.
- The shared step (1.4) **never writes `Torque`** on a player body (kinematic orientation).
- **Note:** `PhysicsComponent.Layer`/`BodyType`/`CollisionDetection` are ctor-only
  (`Swordfish/ECS/PhysicsComponent.cs:11-16`); codecs never fabricate a `PhysicsComponent` value. The
  **server owns body construction** at spawn.
- Engine `PhysicsComponent` is **not** modified — canonicalization is game-side only
  (`PhysicsMessage`/`PhysicsCodec`); no game-specific engine edit (commit boundary).
- **Acceptance:** snap payload uses canonical fields; `PhysicsCodec.Apply` preserves the body; no
  player path writes torque.

### 1.4 [S] Shared simulation step — tick-tagged commands, driven per physics step (new project)
`[x]` New shared gameplay project **`WaywardBeyond.Shared.Gameplay`** (`net9.0`; refs `Swordfish`,
`Swordfish.ECS`, `Shared.Networking`/`Shared.Data`), referenced by both client and server. Contains:
- **Per-world instance, not a DI singleton** — it holds per-world sim state (command staging, look
  accumulation, clamp base). The client world and server world each resolve their own instance; a
  shared singleton would corrupt both.
- **Per-physics-step execution:** drive from `JoltPhysicsSystem.FixedUpdate`
  (`JoltPhysicsSystem.cs:33,130-131`) — the solver already fixed-steps at 0.016s with its own
  accumulator. The shared step runs inside that step, so force application and `System.Update` are in
  lockstep by construction. **No second independent accumulator** (two would phase-drift).
- **Tick-tagged commands (canonical sim tick = physics-step ordinal, decision 6):** each
  `InputComponent` carries its target **sim tick** (the sample's `ServerTickAtSample`). On the server,
  **input is staged per sim tick** (`NetworkReplicationSystem.ApplyComponent` no longer overwrites the
  entity's latest input; it bumps a per-entity tick→command queue) and the shared step consumes the
  staged command for the current sim tick — exactly one command per step, newest-for-tick on collision.
  Client `PendingInputComponent` becomes **sim-tick-keyed** (`AckUpTo` trims on sim-tick ordinals);
  live prediction selects the newest command for the phantom current sim tick, estimated by advancing
  **one sim tick per physics step** from the last acked snapshot. Reconcile replay (1.8) obeys the same
  command-per-step mapping.
- **Shared player-motion step.** Query contract keys on **`InputComponent + PhysicsComponent +
  TransformComponent`** (all shared/engine types — no client `PlayerComponent`, no `Session`-keyed
  uniqueness). Per entity:
  - **Look:** set `TransformComponent.Orientation` directly from the resolved gimbal-free look
    quaternion carried by the command (`LookX/Y/Z/W`). Both prediction and the server apply the
    identical value, so the result is deterministic (no per-step clamp; angular anti-cheat is out of
    scope). Never writes `Torque`.
  - **Movement:** compute forces from `Movement` + orientation-derived `GetForward/Right/Up`
    (`PlayerControllerSystem.cs:144-190` behavior) → `PhysicsComponent`, carrying `BASE_SPEED`,
    `DECELERATION`, jump handling. Pin `Jump` one-shot vs hold semantics.
- **Shared player-body config fragment:** the capsule + `PhysicsComponent` params
  (`Layers.MOVING`, `BodyType.Dynamic`, `CollisionDetection.Continuous`,
  `PlayerCharacterEntityBuilder.cs:23,30-31`) plus movement constants live here so the client builder
  and the server authority builder construct identical bodies. The actual capsule `CompoundShape`
  construction (`PlayerCharacterEntityBuilder.cs:30-31`) gets the same **authority/view split** as the
  voxel builder (4.3) — the server authority builder creates the same body without render/model
  coupling.
- `NetworkReplicationSystem` snapshot `TickNumber` publishes the **sim tick at publish** (not the
  server per-world tick counter).
- Server runs it as authority; client runs it as prediction (live + reconcile replay).
- **Acceptance:** deterministic given identical input sequences at the shared 0.016s step; used by
  both sides; no duplicated movement/rotation/body-config code remains client-only.

### 1.5 [G] Delete the client-authoritative rotation path
`[x]` In `Client.Core/Systems/PlayerControllerSystem.cs`, remove torque/cursor-queue/Rotate/angular
deceleration (`:112-142`, `:200-203`).
- **Relocate the stranded responsibilities** explicitly:
  - Mouse capture / window-focus handling (`SetInputEnabled`, `OnWindowUnfocused`, cursor lock) — move
    to the surviving input/camera sampler (e.g. `ClientInputSystem` or a slim capture owner).
  - `PlayerMovedEvent` emission (`PlayerControllerSystem.cs:109`) — fire from the client's prediction
    step so `PlayerMovedStatisticListener` (stats persistence) keeps working.
- The camera reads `TransformComponent.Orientation` directly (no smoothing; local smoothness comes from
  prediction ticking).
- Kinematic orientation rides the existing entity-wins sync cycle: `SyncEntityToJolt` pushes entity
  rotation into the body and `SyncJoltToEntity` reads it back each step (`JoltPhysicsSystem.cs:250-251,307`).
  The only requirement is that nothing writes `Torque` on the player — which 1.3/1.4 guarantee.
- **Acceptance:** no code path rotates the player via torque; focus/cursor handling and movement stats
  still work.

### 1.6 [E1+E2, then G/S] Server runs simulation (with ordering)
`[x]` Wire `ServerContext` (`Server.Core/ServerContext.cs`) with the shared step and physics. **Commit
order is engine-first:** `[E1]` then `[E2]` land as separate engine commits (standalone green), then
the `[G/S]` game commits consume them.
- **[E1] Engine commit — once-only `Foundation.Init` guard.** `Foundation.Init` is **not idempotent**;
  two `PhysicsSystem` worlds in one process are safe. Hoist `Foundation.Init` + the DEBUG assert/trace
  handlers behind a once-only static guard in `JoltPhysicsSystem` (`JoltPhysicsSystem.cs:55-75`).
  *Justification:* generic multi-world hosting; required so a second physics world can exist in the
  process.
- **[E2] Engine commit — construction of a second physics world.** `JoltPhysicsSystem` is `internal`
  and registered `Reuse.Singleton` (`EngineContainer.cs:84`), so game code cannot obtain a second
  instance bound to the server store. Expose a generic factory/registration path for a dedicated
  `IPhysics` instance (the server's), separate from the engine's client singleton, whose `FixedUpdate`
  the shared step can subscribe to. *Justification:* generic engine capability, not game-specific.
- **[G/S] Game commits** on top of that API:
  - Register the second `JoltPhysicsSystem` on `ServerContext.World`; register the shared step (1.4)
    against its `IPhysics.FixedUpdate`.
  - `ServerContext` gains access to engine DI for these systems — the first locality change; keep the
    class host-agnostic (no window/render types) and use a **server-specific system composition** (not
    the engine's `IEntitySystem[]`, which includes render-coupled systems).
  - Guard the server tick loop with try/catch/log: the Jolt assert handler throws
    (`JoltPhysicsSystem.cs:73`), and an unhandled assert would silently kill the `Server` thread.
  - **Mirror the client's physics runtime config** on the server world: gravity zero
    (`physics.SetGravity(Vector3.Zero)` — the client does this at `Entry.cs:72`; a fresh
    `PhysicsSystem` defaults to Earth gravity), same `PhysicsSettings`/`physics.toml`, same
    layer/collision filtering. Otherwise server authority diverges from client prediction on the first
    tick.
- **Server system order must be explicit** (registration order = tick order; the shared step executes
     inside `JoltPhysicsSystem`'s accumulated step via `FixedUpdate`):
     1. `ServerSpawnSystem` (create sessions/entities + bodies)
     2. `NetworkReplicationSystem` **Apply stage** (drain + apply inbound, **staging inputs per sim
        tick**, `NetworkReplicationSystem.cs:41-81` — split it)
     3. `JoltPhysicsSystem` (accumulates; each fixed step fires `FixedUpdate` → shared player-motion step
        consumes the staged command for the current sim tick → `System.Update`)
     4. `NetworkReplicationSystem` **Publish stage** (collect dirty + removed → send; `TickNumber` =
        sim tick at publish)
     Otherwise snapshots go stale by a tick.
- **Acceptance:** `TransformComponent`/`PhysicsComponent` dirtied server-side replicate downstream
  through the existing `WorldSnapshot` path (uuids 2/3 already registered) with **zero new replication
  code**.

### 1.7 [G] Authority flip — echo authoritative state to every client
`[x]` Remove the owned-player transform-echo skip (`NetworkReplicationSystem.cs:181-186`):
- `NetworkReplicationSystem` publishes authoritative state to **all** clients including the local one.
- `ServerPlayerOwnership` is removed; its role is replaced by session routing (Phase 3).
- Must land *with* 1.4/1.8/1.9 so the local player never visibly stutters.

### 1.8 [G] Make reconciliation real
`[x]` Update `Client.Core/Systems/ClientReconcileSystem.cs`:
- Apply authoritative `Transform`/`Physics` snapshot (**full state: position, orientation, linear AND
  angular velocity** — snap before replay).
- Trim `PendingInputComponent` by `WorldSnapshot.LastProcessedInput` (existing `AckUpTo`, now on
  **sim-tick ordinals**, decision 6).
- Replay remaining inputs through the **shared step (1.4)** — absolute yaw/pitch makes replay exact,
  using the same command-per-step mapping as the server.
- **Pin the replay collapse rule:** as the simulated sim tick advances, **drop earlier commands for the
  same sim tick** — replay must match the server's newest-for-tick staging exactly, not replay the full
  pending buffer (otherwise replay over-applies and over-corrects).
- `SnapshotAckTracker` already records applied ticks; keep echoing via `ServerTickAtSample` (sim tick).
- **Client system order:** reconcile (apply incoming) → sample input (push pending) → replicate dirty
  client-owned → predict via shared step → render reads final `Transform`. Verify empirically.
- **Acceptance:** after server + replay, client state matches what the server will compute. A
  "client prediction == server result (modulo unacked input)" test passes.

### 1.9 [G] Spawn handshake inversion
`[x]` Under authority the **server** assigns the initial transform: delete the client's
placement-upstream flow (`ClientPlayerSpawnSystem.SendInitialTransform`, `ClientPlayerSpawnSystem.cs:90-106`)
and `NetworkReplicationSystem.ApplyPlacement`'s client-authored-transform acceptance
(`NetworkReplicationSystem.cs:141-163`). The client adopts the server-spawned transform (restored
location or `Level.Spawn`).
- **Acceptance:** no client-authored `ServerOwned` state is accepted; spawn position originates
  server-side.

### Phase 1 acceptance (gate)
`[~]` In singleplayer (loopback): behavior visually near-identical to today, but the server now provably
holds authority — verified by deliberately drifting client state and watching reconcile correct it.
The headless determinism + authority tests pass; the live loopback visual check is pending on Windows
(Reef/window runtime).

---

## Phase 2 — Server voxel world + remote player visuals

### 2.1 [S/G] Server loads save data and builds voxel colliders
`[ ]` Builds on Phase 1's server Jolt world:
- **Collision-shape derivation comes first (`[S]`):** split it out of `VoxelObjectBuilder.Build`
  (`VoxelObjectBuilder.cs:41` produces `CollisionShape` together with `OpaqueMesh`/`TransparentMesh` in
  render-coupled client code). The shared derivation builds the shape from `VoxelObject` voxel data;
  the server authority builder and the client view builder both consume it. `VoxelEntityData` (the
  persisted/streamed DTO) contains only voxel chunks — no collision shape — so this is required, not
  optional.
- Reuse/port the voxel entity data flow (`VoxelEntityData` DTOs, `VoxelObject`, chunk model) into the
  server so it can construct world entities and their Jolt colliders (`ColliderComponent`,
  `VoxelEntityBuilder.cs:114`) without any mesh/render components.
- Needs support from Phase 4's `WorldSaveService` to read `levels`; for Phase 2, a server-side read of
  the `levels` bucket or load-stage is acceptable interim.
- **Acceptance:** server authority resolves movement against voxel structures (no longer arena-only).

### 2.2 [S/G] Remote player visuals (public character view)
`[ ]` Relay a **minimal public character view** so clients can materialize remote players:
- New replicated data on the server player entity carrying `CharacterId`, `Name`, `Body` (appearance
  index). Add `PlayerViewComponent` (or similar) with a stable uuid + ServerOwned codec — payload is a
  new/derived shared nsd message; goes through the existing dirty/snapshot path.
- The public view originates from the joining client (characters are client-owned) and is uploaded in
  the join request (Phase 3/4) then relayed server-side to all clients.
- **Never** transmit inventory/attributes/statistics. Client builds the remote player's model from
  `Body`.
- **Acceptance:** two in-process clients see and render each other as correct models; no progression
  data crosses the wire.

### 2.3 [S/G] World-body replication + shared structure dynamics (decision 16)
`[ ]` World/voxel bodies are **Dynamic and server-authored** (`VoxelEntityBuilder.cs:66` already
creates `BodyType.Dynamic`); their motion must replicate:
- Server world entities join the snapshot path: give them the replication marker so
  `NetworkReplicationSystem`'s `NetworkComponent` gate covers them (they currently carry none). Their
  `TransformComponent`/`PhysicsComponent` (uuids 2/3, already ServerOwned) then replicate per tick —
  no new codecs.
- Move `ThrusterSystem` (and any structural force systems) into the **shared deterministic step**
  (1.4) so both worlds apply identical forces; the server is authoritative and post-impulse structure
  state flows down with the snapshots. `ThrusterSystem` currently applies
  `GetForward() * -(Power * 10 * delta)` once per ECS tick (`ThrusterSystem.cs:22`); in the shared step
  it must evaluate **per physics step at the fixed `0.016s` delta** — a cadence change, deterministic
  on both sides.
- Clients snap authoritative structure state each tick (drift corrected like the player); client
  structure bodies simulate the shared dynamics for prediction feel, and snapshots snap any residual
  drift.
- Voxel **content** mutation stays out of scope (decision 16).
- **Acceptance:** a thruster-driven or impulse-pushed structure moves identically in server authority
  and client view, corrected by per-tick snapshots.

---

## Phase 3 — Multi-client sessions & disconnect (LAN)

### 3.0 [G] Connection hub & per-session ack
`[ ]` Every transport/interface is single-pair today (`INetworkTransport`/`IClientConnection`/
`IServerConnection` have no addressing; `ServerContext.cs:23` and `NetworkReplicationSystem` hold one
`IServerConnection`; `LocalConnection` is one client+server pair; `TcpTransport.Listen` accepts one
peer, `TcpTransport.cs:40-47`). Model N clients explicitly:
- **`ServerConnectionHub`** (server core) owns a **set of per-client `IServerConnection`s**;
  `ServerContext`/`NetworkReplicationSystem` construct against the hub instead of a single connection.
- **Per-session ack:** the [`WorldSnapshot.LastProcessedInput`](../NETWORKING.md) single field can't
  carry N clients' acks, so the server composes a **per-client `WorldSnapshot`** each tick — the same
  broadcast component set (small N), but `LastProcessedInput` = that client's value, derived from its
  entity's `LastAckedInput`.
- `SessionManager` mapping becomes **session ↔ connection ↔ entity**.
- `TcpTransport.Listen` gains a **multi-peer accept loop** (one endpoint per accepted peer, added to
  the hub); `LocalConnection` gains a multi-client test fixture (N client endpoints → one server hub)
  for the N-client tests. Client side stays one `IClientConnection` per client.
- **Acceptance:** the hub serves N connected clients; each receives per-client acks; the N-client test
  fixture exercises routing without a socket.

### 3.1 [G] Sessions replace single ownership
`[ ]` Wire `SessionManager` (`Server.Core/SessionManager.cs`) + `Session`:
- `ServerSpawnSystem` (`Server.Core/Systems/ServerSpawnSystem.cs:47`) allocates a session per
  connection and sets `NetworkComponent.Session` instead of `ServerPlayerOwnership.SetOwnedPlayer`.
- Replication rules key off session → connection → entity (a remote LAN client *does* receive its own
  authoritative transform; the local player behaves identically).
- **Acceptance:** N synthetic clients can spawn and be routed concurrently over the connection hub /
  `LocalConnection` fixture.

### 3.2 [G] Disconnect handling
`[ ]` None exists today. On disconnect: end session, free client mirrors, remove mappings, **dispose
`PhysicsComponent` bodies** (they are `IDisposable` and post body-destroy to their `ThreadContext`)
before `store.Free`, and replicate despawns via the existing `WorldSnapshot.RemovedEntities` path
(`NetworkReplicationSystem.cs:195-209` already picks up remote despawns of server entities).
- **Acceptance:** dropping a client removes its entity server-side and clients observe the despawn.

### Design note
Downstream join/streaming (Phase 4) is designed against N connected clients from this phase onward;
singleplayer is just the N=1 case over the same join path.

---

## Phase 4 — State ownership & join streaming

**Goal:** the client no longer owns world generation, world save/load, or the live world. The server
owns the `levels` bucket, world gen, the authoritative voxel world, and per-character location
persistence. Clients own only `characters`. World data is streamed to the client during join.

### 4.1 [S/G] World generation moves server-side
`[ ]` Re-host world-gen into shared/server code:
- `WorldGenerator`, `WorldGenNewGameStage`, `StarterShipNewGameStage`, and deps (`BrickDatabase`,
  `VoxelObjectBuilder`) move out of `Client.Core`. **`VoxelObjectBuilder` splits** (per 2.1/4.3): the
  collision-shape derivation becomes shared `[S]`; the mesh/render half stays client-side.
- New nsd pair: `NewWorldRequest { Name, Seed, GameMode }` / `NewWorldResponse { LevelGuid, Success }`
  — server generates into its world, persists Level meta + voxel entities to `levels`, replies.
  (`GameSaveService.CreateSave` currently hardcodes `GameMode.Creative`, `GameSaveService.cs:158` —
  the request must carry the mode.)
- Client `NewSavePage`/new-save UI calls the server instead of generating locally.
- `BrickDatabase`/brick definitions become shared or server-hosted (currently client-only, registered
  `Injector.cs:255-272`).
- **Acceptance:** a "new game" from the UI produces a server-authored world; `WorldGenerator` no
  longer runs client-side.

### 4.2 [G] Server `WorldSaveService` + client save contraction
`[ ]` Server-side counterpart to `GameSaveService` over the **server** ECS:
- Reads/writes `levels` KV: `Level` meta + `VoxelEntityData` + per-character location
  (`<guid>.character.<id>`), world-gen-if-empty, retain the legacy disk-migration path.
- **Structure transforms are sampled from the server's authoritative world** (decision 16) — the
  saved `VoxelEntityData` position/orientation reflects server-side dynamics, not client state.
- Auto-save on the server's own schedule and **flush on server stop** (client exit triggers server
  stop in SP). **Throttle/submit the save to a worker**: `KeyValueStore` is sync-over-async (blocks on
  `.Task.Result`), so an inline full-world save would stall the server tick loop mid-physics.
- Per-character location is sampled from the **server's authoritative transform** on interval,
  disconnect, and shutdown — not from client state.
- **Shutdown cascade (defined and mechanically enforced):** client window close → client requests
  server stop → server flushes world save → server thread exits → NATS process stops → process exits.
  Shoal dispose order (`IEntryPoint` vs `IAutoActivate`) is unspecified, so the flush must be **awaited
  before `PersistentNatsProcess` dispose** (an explicit sequencing point in SP teardown), not assumed.
  Ownership of each sequencing step (server stop before NATS dispose) is explicit.
- **World-switch:** when the menu changes saves, the server unloads the current world and **disposes
  its physics bodies** (reuse the 3.2 dispose path) before loading the new world and re-streaming.
- Client `GameSaveService` shrinks: character save handled by `CharacterSaveManager`; save **listing**
  becomes a query to the server (replaces direct `GetKeys`/`Level.Deserialize`).
- `GameSaveManager` keyboard/window triggers stay client-side but delegate world-saving to the server.
- **Acceptance:** world persistence is served entirely from the server world; client holds only
  `characters`.

### 4.3 [S/G] Split the entity builders
`[ ]` `VoxelEntityBuilder` becomes two variants:
- **Authority** (server): ECS entities + colliders, no mesh components (used in Phase 2).
- **View** (client): ECS entities + meshes **+ local colliders kept for prediction** (locked decision
  9); server reconcile corrects.
- **Collision-shape derivation lives in shared `[S]` code** (extracted out of `VoxelObjectBuilder.Build`,
  which also produces meshes, `VoxelObjectBuilder.cs:41`); both variants derive colliders from the same
  shared code, so authority and prediction shapes match. The server never meshes.
- The authority variant skips the per-voxel child entities + decorators (`VoxelEntityBuilder.cs:165-177`)
  — those exist only for rendering/lighting.
- **World bodies stay Dynamic and their motion is replicated** (decision 16, task 2.3): clients
  simulate the shared dynamics for prediction feel and **snap authoritative structure state** each
  tick; voxel content mutation is out of scope.
- `VoxelEntityLoadStage`/`CharacterEntityLoadStage` become consumers of the join stream instead of KV
  readers; the character-location-restore logic moves into the server join handler.
- **Acceptance:** the client renders and predicts against streamed world data while the server holds
  the collider- and dynamics-authoritative copy.

### 4.4 [G] Join handshake + full-world stream (nsd)
Extend the spawn/join flow (Phase 3 + 1.9):
- **Client join orchestration:** a `ClientJoinSystem` drives `GameState.MainMenu → Joining → Playing` and
  replaces the `GameSaveService.Load`/`RequestSpawn`-driven flow; the old
  `ClientPlayerSpawnSystem`/`ServerSpawnSystem` are retired or reworked into the join handlers.
- `JoinRequest` (extends/replaces `SpawnRequest`) `{ LevelGuid, CharacterId, PublicView }` →
  server loads/builds the world, restores `<guid>.character.<id>` (or `Level.Spawn`), accepts →
  streams: `JoinAccept { Level meta, SpawnTransform, roster }` + **per-entity**
  `WorldEntityAdd { VoxelEntityData }` messages + `WorldStreamComplete`. Each entity message is bounded
  to its per-entity KV payload, parses incrementally, and enables progress reporting (no NATS/nsd
  message-size ceiling from a single batch).
- Client builds view entities from the stream as each `WorldEntityAdd` arrives, then seats its player
  at the server-assigned spawn. **View-entity building runs on the client ECS thread** (a system
  polling `WorldEntityAdd`), not the menu/UI thread — today's load stages run on the UI path
  (`GameSaveService.Load` is awaited from the menu flow) and mutate the DataStore there; streaming is
  the time to fix that thread discipline.
- **Join ordering gate:** per-type transport queues don't preserve cross-type order (no envelope), so
  the client defers `WorldSnapshot` application (`ClientReconcileSystem` no-ops) until
  `WorldStreamComplete` arrives.
- Subsequent per-tick replication keeps using the existing `WorldSnapshot` path — streaming is a
  join-time event, not ongoing AOI.
- **Acceptance:** a client joins a server, receives the full world, renders it, and plays; a returning
  player spawns at their persisted location.

### 4.5 [G] Client character ownership & public view
`[ ]` Keep `ICharacterStorage`/`NatsCharacterStorage` (`characters` bucket) client-owned unchanged —
**no `EquipmentComponent` persistence, no new `Character` fields** (locked decision 11).
- The only character data transmitted is the minimal public view (Id, Name, Body) from 4.4/2.2.
- Client character menus (`SelectCharacterPage` etc.) stay local; `SelectSavePage` lists worlds from
  the server.
- **Acceptance:** inventory/attributes/statistics are absent from every wire message.

### 4.6 [S] Consolidate duplicated DTO/serializers
`[ ]` Unify client `VoxelEntityModelSerializer`/`CharacterEntityModelSerializer`
(`Client.Core/Serialization/`) with shared `VoxelEntityData`/`CharacterEntityData` so **streamed ==
persisted** payloads; audit for client-only dependencies so they can run server-side.
- **Precision:** pin one convention — `VoxelEntityData`/`CharacterEntityData` use `double` positions
  while the runtime `TransformComponent`/`TransformMessage` use `float`; the consolidated DTO decides
  (recomend `float` to match runtime transforms).
- **Acceptance:** one set of world DTOs serves persistence and streaming; server has no client-only
  serialization refs.

---

## Phase 5 — Transport, LAN, and the dedicated-host seam

### 5.1 [G] Transport selection as a DI/decision
`[ ]` Replace the hardwired `LocalConnection` delegates (`Client.Core/Injector.cs:130-132`) with a
host-mode selection: `LocalConnection` = default singleplayer; `TcpTransport` = when hosting/joining
over a socket. `IsLocal` shrinks to plumbing, never a simulation fork.
- **Acceptance:** singleplayer boots through the same selection path LAN will use.

### 5.2 [G] `TcpTransport` per-type demux
`[ ]` `TcpTransport` currently has one shared `_receiveQueue` (`Transport/TcpTransport.cs:19`), but
systems poll distinct types — frames would be mis-delivered. Add a local length + type-tag frame
boundary and per-type dispatch (mirroring `LocalConnection`'s per-type queues). Transport framing only —
**no ack/ordering/reliability**.
- **Acceptance:** two in-process client/server contexts can run over TCP with no cross-type frame
  stealing. This is the first real exercise of the peer path.

### 5.3 [G] Server as a real Shoal module (dedicated-host seam)
`[ ]` Add `Server.Core/manifest.toml` (mirror `Client.Core/manifest.toml`, ID
`waywardbeyond.server.core`), remove the hard-wired `ServerComposition.Register` call
(`Client.Core/Injector.cs:143`), and let the server module load via the standard module path
(`AppEngine`/`ModulesLoader`). The launcher's `modules.toml` already lists
`waywardbeyond.server.core`.
- **Runtime packaging:** `Server.Core.csproj:22` references `Swordfish` with `ExcludeAssets="runtime"`
  (compile-only; the client provides the engine runtime in-process). A standalone host must ship the
  engine runtime with the server module — flip that for the module packaging so a future host can run
  the shared step + Jolt.
- **Acceptance:** server boots through module discovery; a future headless host is a thin shell
  reusing `ServerContext` unchanged. Launcher is **not** built in this initiative.

---

## Phase 6 — Docs & validation

### 6.1 [G] Documentation
`[x]` `NETWORKING.md` rewritten to describe the actual architecture; all envelope content removed.
Prediction acks retained and documented as gameplay-level (not transport).
`[~]` Keep `NETWORKING.md` accurate as phases land (join/stream messages, `InputComponent` field
names, new systems).
`[ ]` Update `AGENTS.md`/this doc as plan details change.

### 6.2 [G] Integration tests
`[ ]` Headless, cross-platform (no window; works on Linux CI). Boot client + server worlds
in-process over `LocalConnection`:
- **Fixed-step harness** drives the shared step with the accumulator (not `World.Tick` wall delta).
- Authority round-trip: server mutates state → snapshot → client reconcile.
- Prediction determinism: replay pending inputs through the shared step equals server result at fixed
  `h`.
- Session routing: N synthetic clients spawn concurrently (Phase 3).
- Join streaming: full-world stream → view entities allocated → player seated at restored location
  (Phase 4), using a **meshless test double** (or the collider-only authority builder). Mesh
  construction is render-coupled and explicitly scoped out of headless CI.
- TCP peer smoke once 5.2 lands.
- Verify `JoltPhysicsSharp` native assets load on Linux CI (Phase 1 task, not an assumption).

### 6.3 [G] LAN smoke
`[ ]` Two in-process client contexts over `TcpTransport` + `ServerContext` listening; verify join,
stream, input, remote-player visuals, and replicate flows (peer path first real use).

---

## Risk register

| Risk | Mitigation |
|---|---|
| **Input/step attachment mismatch** breaks determinism | Tick-tagged commands, one per **canonical sim tick** (physics-step ordinal) via `FixedUpdate` (1.4, decision 6); server stages per-sim-tick commands; both sides apply the identical mapping; test harness drives the shared step through the physics accumulator |
| Two-clock ambiguity (replication tick vs physics step) | Rebase `TickNumber`/`LastProcessedInput`/`ServerTickAtSample`/`LastAcked*` on the sim tick (decision 6); snapshot publishes sim tick at publish |
| Replay applies commands the server collapsed (over-apply) | Pin the newest-per-sim-tick collapse rule in replay (1.8) |
| Server physics world diverges from client (default gravity etc.) | Mirror client runtime config: `SetGravity(Vector3.Zero)`, same `PhysicsSettings` (1.6) |
| Dynamic world-body replication load / per-client snapshot cost | Dirty `Transform`/`Physics` per tick only (2.3); small N + few structures; per-client `WorldSnapshot` composition bounded by N (3.0) |
| Structure motion diverges between authority and prediction | Shared dynamics step on both worlds + per-tick authoritative structure snapshots (2.3, 4.3, decision 16) |
| Stale-structure-pose during player prediction | Reconcile corrects; client snaps authoritative structure state each tick |
| Client/`WorldEntityAdd` build on the wrong thread | View-entity building is a client ECS-thread system, not the menu/UI path (4.4) |
| Full-world stream exceeds transport/NATS message limits | Per-entity `WorldEntityAdd` framing (decision 8, 4.4); each message bounded to its KV entity size |
| Collision shapes only derivable in render-coupled client code | Extract shape derivation to shared `[S]` (2.1, 4.3); server never meshes |
| Server module can't run standalone without engine runtime | Flip `ExcludeAssets="runtime"` for the server module (5.3) |
| Shutdown flush races NATS teardown (Shoal dispose order unspecified) | Explicit flush-complete-awaited-before-`PersistentNatsProcess`-dispose sequencing point (4.2) |
| World-switch leaks server physics bodies | Reuse the 3.2 `PhysicsComponent` dispose path on world unload (4.2) |
| Second `JoltPhysicsSystem` in-process (Foundation non-idempotent) | `[E1]` once-only `Foundation.Init` guard landed as its own engine commit before the server instance (1.6) |
| Two concurrent physics worlds abort (shared temp allocator) | JoltPhysicsSharp `Update(dt, steps, jobSystem)` funnels every solve through one function-local `TempAllocatorImplWithMallocFallback`; two worlds solving on two threads corrupt it → SIGABRT. `[E3]` serializes native solves across worlds with a shared lock (1.6). Upstream exposes no per-system allocator through 2.22.0 |
| Engine/game entanglement blocks cherry-picking into the engine project | Separate engine commits, committed first, standalone green; every `[E]` change justified in the engine change register |
| Server thread dies silently on physics/ECS exception (Jolt assert throws) | try/catch/log guard around `ServerContext.Update` (1.6) |
| Cross-type ordering at join (per-type transport queues, no envelope) | Client gates snapshot application until `WorldStreamComplete` (4.4) |
| Player Jolt body re-introduces torque integration, reverting look feel | 1.3/1.4 never write `Torque`; existing sync cycle makes orientation entity-wins; test asserts orientation tracks the shared step output |
| Authority flip (1.7) lands without prediction → local player stutters | Land 1.4+1.7+1.8+1.9 in the same commit/window |
| Prediction drift from clamp/validation asymmetry | All clamps in shared code (1.4); replay uses the same step + same command-per-step mapping (1.8) |
| Sensitivity accidentally duplicated into shared code | 1.1/1.2 keep it client-only by construction; grep review |
| Client keeps colliders (decision 9) → dual-physics divergence/cost | Document that client colliders are for prediction only; reconcile owns corrections; watch CPU |
| Server voxel world (Phase 2) depends on `levels` data before Phase 4 completes | Interim server-side `levels` read/load-stage acceptable; finalized by 4.2 |
| NATS single instance shared client+server in-process | No change now (both buckets on same local NATS); if a dedicated server runs its own NATS later, `characters` stay client-local — note it |
| Sessions phase (3) precedes transport demux (5) | Validate N sessions over the connection hub + `LocalConnection` fixture (3.0); real LAN smoke waits for Phase 5, where it lives |
| In-process hidden state sharing (statics, e.g. `NetworkRegistry`) across worlds | Document shared statics; keep server/client code paths free of raw cross-`DataStore` access |

## Engine change register

Every `[E]` engine change in this plan, its justification, and why it cannot live in game code. Each
must land as its own engine commit, committed **before** the game commits that consume it.

| Ref | Change | Why it must be engine | Justification (generic/reusable) | Game-specific? |
|---|---|---|---|---|
| `[E1]` | Once-only `Foundation.Init` (and DEBUG assert/trace handler) guard in `JoltPhysicsSystem` ctor | `Foundation.Init` (JoltPhysicsSharp) is non-idempotent; game cannot guard without touching the engine ctor that calls it | Any host running two physics worlds in one process needs it — a general engine capability, not WaywardBeyond-specific | No |
| `[E2]` | Expose construction of a second physics `IPhysics`/`JoltPhysicsSystem` instance separate from the engine's singleton (`EngineContainer.cs:84` registers it internal + `Reuse.Singleton`) | The type is `internal` and registered app-singleton; game code cannot obtain a second instance bound to the server world | Hosting an additional physics world is a general engine capability; keeps the game host-agnostic | No |
| `[E3]` | Serialize native physics solves across worlds (shared lock around each `JoltPhysicsSystem` fixed step) | JoltPhysicsSharp's `Update(dt, steps, jobSystem)` uses a shared function-local temp allocator; two worlds solving concurrently on two threads corrupt it and `abort()` (diagnosed: SIGABRT in `TempAllocatorImplWithMallocFallback::Free`). Game cannot fix it without touching the engine's solve call | Hosting multiple physics worlds that update concurrently in one process is a generic engine capability; the binding exposes no per-system allocator (through 2.22.0) | No |

If a future task needs another `[E]` change, it must first attempt a game/shared-side implementation and
add the row here with justification before committing.

## Open items (tracked, non-blocking)

- Pause semantics: the server thread keeps ticking while the client pause menu is open. Revisit before
  LAN hosting (SP may pause; hosts may not).
- Wiring `NetworkComponent.ServerTPS` to advertise the server's **physical step rate** so clients can
  align the estimated sim tick (supports 1.4/decision 6).
- Who launches the local NATS process in a future dedicated layout (today client `Entry` starts it).

## Verification commands

```bash
dotnet build                                        # compiles all projects (runs nsdc codegen)
dotnet test Swordfish.Tests                         # engine tests (xunit, cross-platform)
dotnet test WaywardBeyond.Client.Core.Tests         # game tests (NUnit)
```

`Reef.Tests` and full engine runtime remain Windows-only (Silk.NET OpenGL window) — networking
integration tests must run headless to stay CI-friendly.