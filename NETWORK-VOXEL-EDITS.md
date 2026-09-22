# NETWORK-VOXEL-EDITS

Development plan and tracker for moving voxel place/break (and, by extension, all player item
interactions) to a **server-authoritative**, network-first model. This establishes the pattern every
future player item interaction will follow.

Status legend: `[ ]` pending · `[~]` in progress · `[x]` done

---

## Purpose

Interactions today are **100% client-local**: `PlayerInteractionService` raycasts against physics
colliders and mutates `VoxelComponent.VoxelObject` directly (`VoxelObject.Set` + `Rebuild`), firing
`PlaceEvent`/`BreakEvent`. The server has no voxel edit path at all, and voxel grids are not networked.

This initiative replaces that with a model in which:

- **The server is the sole authority for interaction *outcome*.** The client never mutates authority
  voxel state; it only presentably *predicts*.
- **The client sends intent, not result.** Interaction edges plus a target *hint*; the server
  independently validates and resolves.
- **A single shared resolve routine** decides "given a ray + held item + game mode → action". Both the
  client (prediction) and the server (authority) call the exact same code. No forked logic.
- **Interactions live entirely in one place — the server.** Mods can introduce/customize interactions
  **server-side only** without any client mod. Moddability is the primary payoff.
- **The client is a dumb renderer for interactions.**

## Locked decisions

1. **Server-authoritative outcome with client presentation prediction** (the A+B layered model).
   The server re-derives/validates every interaction; the client predicts presentably and reconciles.

2. **Full server-thin-client over the interaction *context*.** The server owns the held item,
   equipment/active slot, game mode, and interaction-relevant inventory counts **after join**.
   The client remains authoritative only for its **initial** save: it seeds the server at join (exactly
   the way appearance sync works today), and from that point the server owns the state.
   - Rationale: this is what makes interactions single-location, server-moddable, and anti-cheatable.

3. **Components are migrated, not mirrored.** `EquipmentComponent`, `InventoryComponent`, and
   `GameModeComponent` move into `WaywardBeyond.Shared.Networking` as the single source of truth
   (nsd messages + auto-registered `ServerOwned`). There is **no** client struct + server struct +
   mapper. The UI reads the same components it reads today.

4. **Character data sync uses a separate optional `CharacterSeed` field on `JoinRequest`**, kept
   distinct from the frequent `PublicView` (Id/Name/Body) used for remote rendering. Public *view* and
   authoritative *state* stay conceptually separate.

5. **Client hint + server validate for aim/target.** The client sends its resolved target cell (plus
   shape/orientation for place) as a *hint*; the server independently validates reach, occupancy, and
   plausibility and is the final authority. The fiddly screen-center/reach-around targeting logic is
   ported into **shared code** so prediction and authority pick the same cell from the same ray.

6. **Move `VoxelObject` to `WaywardBeyond.Shared.Gameplay`.** One canonical mutator
   (`VoxelObject.Set/Get`, brick-space conversion) is used by both client prediction and server
   authority. The `VoxelColliderParityTests` move with it and keep guarding coordinate parity.
   Server persistence re-derives `VoxelEntityDataComponent.Chunks` from the shared `VoxelObject`
   via `GetChunkInfos()`.

7. **Prediction is included in the first cut**, using the **same shared resolve and shared apply code
   as the server** (deduplication goal). No divergent client-only interaction simulation.

8. **Transport is assumed ordered/lossless.** No application-level reliability/ordering code. Today the
   transport is TCP (in-process `LocalConnection`); any future UDP would be RUDP and handled at the
   transport layer, never here.

9. **Continuous state rides the existing per-frame `InputComponent` packet**; discrete taps are
   delivered as **latched edge events** so they are lossless even under a throttled send rate. A
   throttle only adds latency, never drops an action.

10. **The interaction event is an extensible pseudo-union of nullable hint sub-messages.** Common edge
    metadata (`Entity`, `SequenceNumber`, `ServerTickAtSample`, `Kind`) lives at the root; per-interaction
    hints are nullable sub-messages (e.g. `BrickInteraction?`, future `AttackInteraction?`). `Kind` stays
    at the root but is **not** the union discriminator — *hint presence* is. A **hint-less event is
    valid** and resolves to `Action.None` (e.g. right-click empty space); code must not assume a hint is
    present.

---

## Architecture invariants

- Client world and server world are separate `World`s/`DataStore`s on separate threads, speaking only
  serialized nsd messages through `INetworkTransport`. No shared-`DataStore` shortcuts.
- The interaction outcome logic is **shared and single-location** — mods extend the server-side
  resolution hook, never client logic.
- All validation/clamps affecting interaction outcomes live in shared code so prediction and server
  resolve identically.
- The client may *predict* a voxel edit presentably, but the **authority voxel state** is only ever
  changed by the server; client prediction is corrected by authoritative `VoxelEditMessage`s.

> **Supersedes `LOCAL-SERVER-SINGLEPLAYER.md` decision 12** ("no equipment persistence; `Character`
> schema is complete as-is"). This initiative extends the `Character` save with `ActiveInventorySlot` +
> `GameMode`, migrates `EquipmentComponent`/`InventoryComponent`/`GameModeComponent` into shared
> `ServerOwned` components, and transmits the interaction-relevant character context from the client's
> local save at join (seeding the server), after which the server owns it.

---

## Current-state snapshot (what this builds on)

- **Interaction is client-local-only.** `PlayerInteractionService` (`Client.Core/Systems/`,
  866 lines) raycasts, mutates `VoxelComponent.VoxelObject`, and rebuilds — with **zero** network
  involvement. It fires local `PlaceEvent`/`BreakEvent` hooks.
- **No server voxel edit path.** No voxel mutation/replication exists in `Server.Core`. The server
  builds structure **colliders** from `VoxelEntityDataComponent.Chunks` at level load
  (`VoxelWorldEntityFactory.CreateAuthority`), but never edits voxels.
- **No downstream voxel-edit replication.** Voxel world entities stream once at join
  (`WorldEntityAdd` per structure, `ServerJoinSystem.StreamWorld`); thereafter only
  `TransformComponent`/`PhysicsComponent` are delta-replicated. `VoxelEntityDataComponent` is
  deliberately **not** in the replication set.
- **Interaction context is 100% client-owned.** `EquipmentComponent`, `InventoryComponent`,
  `GameModeComponent`, `BrickDatabase`, `ItemDatabase`, `PlayerData.GetMainHand` all live in
  `WaywardBeyond.Client.Core`. The server's `OwnedCharacterComponent` + `BodyViewComponent` +
  `IdentifierComponent` mirror only the minimal public view (Id/Name/Body).
- **Character save is client-owned.** `ICharacterStorage`/`NatsCharacterStorage` (`characters`
  bucket) holds the full `Character` including `Inventory` (`ItemData[]`). Server stores only
  per-character *location* (`<guid>.character.<id>` → `CharacterEntityData`). `EquipmentComponent`
  active slot is **not** persisted at all; `GameModeComponent` is hardcoded **Creative**
  (`PlayerCharacterEntityBuilder.cs:29`).
- **Registry/registration.** `NetworkRegistry` auto-registers `[NetworkComponent(uuid, direction)]`
  value-type `IDataComponent` structs; `NetworkRegistry.Initialize(...)` + explicit `Register<T>` in
  `Client.Core/Injector.cs` (`RegisterNetworking`, lines 114-167). Registered today: `InputComponent`
  (1, ClientOwned), `TransformComponent` (2, ServerOwned), `PhysicsComponent` (3, ServerOwned),
  `BodyViewComponent` (10, ServerOwned), `IdentifierComponent` (11, ServerOwned).
- **Input pipeline.** Client samples `InputComponent` per ECS frame (absolute look totals, tick-tagged
  `ServerTickAtSample`) → `PendingInputComponent` ring buffer → `ClientReplicationSystem` publishes
  dirty client-owned → server `ApplyComponent` stages per sim tick in `NetworkComponent.StagedInputs`
  (`InputStageBuffer`) → shared `SharedPlayerMotionStep` consumes one command per sim tick →
  `ClientReconcileSystem` applies authoritative `Transform`/`Physics` and trims/replays pending input.

---

## Non-goals (explicitly out of scope)

- Any application-level reliability/ordering/ack for voxel edits — assumed guaranteed by the transport
  (TCP today; RUDP later). (See `LOCAL-SERVER-SINGLEPLAYER.md` decision 14.)
- Anti-cheat beyond shared-step input clamping and server-side interaction validation.
- Replicating full character progression *continuously* — only the interaction-relevant context
  (equipment, game mode, item counts) is server-owned, seeded at join.
- AOI / interest management — edits broadcast to all clients.

---

## Phase 1 — Move voxel container to Shared

**Goal:** one canonical, parity-guarded voxel container both sides can use.

### 1.1 [S] Relocate voxel container types
`[ ]` Move `VoxelObject`, `ChunkData`, `ChunkVoxel`, `VoxelPalette`, `VoxelSample`, and `Short3`
numerics from `WaywardBeyond.Client.Core/Voxels` into `WaywardBeyond.Shared.Gameplay`.
- Re-point consumers: `VoxelEntityBuilder`, `VoxelObjectProcessor` (render passes), and the tests.
- Keep `VoxelColliderParityTests`/`VoxelObjectTests` compiling against the moved type — they guard the
  negative-coordinate parity behavior.
- Confirm `VoxelObject.GetChunkInfos()` (already present) is the persistence bridge to
  `VoxelEntityDataComponent.Chunks`.
- **Acceptance:** `dotnet build` + `dotnet test WaywardBeyond.Client.Core.Tests` green with the shared
  container; no client-vs-server fork of voxel math.

---

## Phase 2 — Migrate + network the interaction-context components

**Goal:** the server owns the interaction context as replicated, server-authored components.

### 2.1 [S] Move context components into Shared.Networking as nsd components
`[ ]` Move `EquipmentComponent` (active slot), `InventoryComponent` (`ItemStack[]`),
`GameModeComponent` (`GameMode`) into `WaywardBeyond.Shared.Networking.Components`.
- Convert to `public partial struct` + nsd messages in `Shared.Networking/CodeGen/components.nsd` with
  `[NetworkComponent(uuid, ServerOwned)]` → auto-registered (no `Injector` change).
- Save the existing uuids for `InputComponent` (1) / `TransformComponent` (2) / `PhysicsComponent` (3)
  / `BodyViewComponent` (10) / `IdentifierComponent` (11); assign new uuids for the three new ones.
- `ItemStack` ↔ nsd `ItemData` (ID/Count/MaxSize) mapping handled by the component codecs. `GameMode`
  enum already lives in `Shared.Data/CodeGen/saves.nsd`.
- The moved structs must become `public` (the current `EquipmentComponent`/`InventoryComponent` are
  `internal`) so the server can author them.
- **Acceptance:** build passes; client UI (hotbar, inventory, `PlayerViewModelSystem`,
  `ActiveSlotNotificationSystem`) reads the *migrated shared* components unchanged in behavior.

### 2.2 [G] Extend character save with interaction-relevant fields
`[ ]` Extend `Character` in `Shared.Data/CodeGen/saves.nsd` with `ActiveInventorySlot` and `GameMode`
(optional, defaults for backward-compat saved characters).
- Persist/load these in `CharacterSaveManager` + `NatsCharacterStorage` (`characters` bucket).
- Replace the hardcoded `GameMode.Creative` at `PlayerCharacterEntityBuilder.cs:29` with the loaded
  value.
- **Acceptance:** active slot and game mode round-trip through the client's character save; new
  characters default sanely.

### 2.3 [S/G] Character seed sync at join (broaden appearance sync)
`[ ]` Add a separate optional field to `JoinRequest` in `Shared.Data/CodeGen/world.nsd`:
```
CharacterSeed CharacterSeed = <field#>;
```
carrying the full authority seed (CharacterId, Name, Body, InventoryContents, ActiveSlot, GameMode).
Keep existing `PublicView` (Id/Name/Body) for remote rendering.
- **Client** (`ClientJoinSystem.RequestJoin`): attach the seed from the loaded `Character`.
- **Server** (`ServerJoinSystem.HandleJoin`, line 83-153): seed the context on the mirror —
  `Server InventoryComponent`, `EquipmentComponent`, `GameModeComponent` — alongside the existing
  `OwnedCharacterComponent` + `BodyViewComponent` + `IdentifierComponent`. Because all are
  `ServerOwned`, remote clients receive full state via the existing dirty + full-sync
  (`CollectFullStateAction`) paths — **no replication-system edits**.
- **Client reconcile** (`ClientReconcileSystem`): add the 3 new comps to the local-player
  authority-echo handling so the authority overwrites the seeded local state (client is not
  authoritative post-join).
- **Acceptance:** a joining client seeds the server; remote/late-joining witnesses materialize the
  player with full equipment/inventory/game-mode through the standard snapshot path.

---

## Phase 3 — Interaction input transport (continuous + edge)

**Goal:** intent flows upstream losslessly, riding the existing input machinery.

### 3.1 [S] Continuous state on InputComponent
`[ ]` Extend `InputComponent` (components.nsd) with `HeldSlot` (uint), `PrimaryHeld` (bool),
`SecondaryHeld` (bool) — ~+3B/frame riding the existing per-frame packet.
- Populate in `ClientInputSystem` from the (now shared) equipment/inventory.
- **Acceptance:** held slot + primary/secondary held state replicate with the existing input packet at
  negligible marginal bandwidth.

### 3.2 [S] Discrete edge message (extensible hint union)
`[ ]` Add a new `ClientOwned` nsd message in components.nsd:
```
message InteractionEvent
{
    ulong Entity;            // player mirror address (dedupe/routing)
    uint  SequenceNumber;
    uint  ServerTickAtSample;
    byte  Kind;              // PrimaryPressed / PrimaryReleased / SecondaryPressed / SecondaryReleased
    BrickInteraction? Brick; // hint payload, extensions add their own nullable hint sub-messages
}

message BrickInteraction
{
    int  TargetX, TargetY, TargetZ;  // client hint target cell
    byte HintShape;         // place only
    byte HintOrientation;   // place only
}
```
- **Extensible pseudo-union via nullable scalar sub-messages.** `InteractionEvent` carries common edge
  metadata at the root (`Entity`, `SequenceNumber`, `ServerTickAtSample`, `Kind`) plus nullable hint
  sub-messages; future interaction types (e.g. `AttackInteraction?`, entity interactions) add their own
  nullable sub-message without touching existing fields. Backward/forward compatible by construction.
- **`Kind` stays at the root and is NOT the union discriminator.** That role belongs to *hint presence*:
  which hint sub-message is set selects the action payload. `Kind` is the button/edge (orthogonal to the
  hint) and the server may use it for authorization regardless of whether a hint resolved.
- **A wholly hint-less event is valid and must be handled.** Every hint may be absent (e.g. right-click
  empty space with no targetable hint), which is an expected path — the server/resolver consumes it as
  `Action.None` rather than erroring. Callers guard optional hints with `.HasValue` / pattern matching.
- Client keeps a **latched coalesced edge queue** (mirroring `PendingInputComponent`) so a tap falling
  entirely in a throttle gap is still delivered on the next packet — lossless, only latency trades.
- Because it is a `ClientOwned` component, `ClientReplicationSystem` (lines 53-69) auto-publishes it.
- **Acceptance:** discrete press/release edges are lossless across a dropped frame; deduplicated by
  `SequenceNumber`; a hint-less event round-trips and resolves to `Action.None`.

### 3.3 [S] Server staging of interaction events
`[ ]` Extend `NetworkReplicationSystem.ApplyComponent` (the `InputComponent` branch, lines 177-199) to
stage `InteractionEvent`s into a new `InteractionStageBuffer` on `NetworkComponent`
(`StagedInteractions`), keyed by `ServerTickAtSample`, collapse newest-per-tick, dedupe by
`SequenceNumber`. Mirror the `InputStageBuffer` implementation.
- **Acceptance:** staged interaction events are consumed exactly once per sim tick, newest-for-tick on
  collision, in a unit test (like `SocketReplayTests`-style).

---

## Phase 4 — Shared resolution + server authority (core)

**Goal:** one shared resolver; the server is the sole author of voxel edits.

### 4.1 [S] Shared interaction resolver
`[ ]` New `SharedInteractionResolver` in `WaywardBeyond.Shared.Gameplay`:
```
input: ray, target cell hint, held item, game mode, reach
output: { Action None | Break | Place, coordinate, voxel }
```
- Port the target-selection heuristics (`TryGetBrickFromScreenSpace`, reach-around, offset/march-back)
  into shared code so client prediction and server authority pick the same cell from the same ray.
- Rules: within reach; break requires occupied cell (or reach-around); place requires destination cell
  empty.
- **Hint-less events are first-class:** the resolver consumes a valid event with no hint (and one where
  the hint does not resolve to a legal target) as `Action.None` — it must not throw or assume a hint is
  present.
- **Acceptance:** resolver parity unit tests — identical (ray, hint, item, mode) → identical action on
  both sides; a hint-less/empty-target interaction resolves to `Action.None`.

### 4.2 [S/G] Server interaction system
`[ ]` New `ServerInteractionSystem` (server tick between `ApplyStage` and `PublishStage` in
`ServerContext.Update`):
- Build authority ray from the mirror's authoritative `TransformComponent` + the `Look` from the
  shared step (already applied server-side).
- Call `SharedInteractionResolver` → validate reach/mode/item.
- Make `BrickDatabase`/`ItemDatabase` resolvable server-side (currently client-only, registered
  `Injector.cs:255-272`) so placeable/loot resolution happens on the server.
- Break/place on the structure's shared `VoxelObject` (Phase 1), then **rebuild that structure's
  `ColliderComponent`** (`VoxelColliderBuilder`) server-side so subsequent raycasts see the change.
- Survival: consume/grant from the server-owned `InventoryComponent`.
- Re-derive `VoxelEntityDataComponent.Chunks` from the shared `VoxelObject` and mark dirty for save
  (`WorldSaveService`).
- **Acceptance:** survival consumption correct; creative is free; collider updates after an edit;
  resolver + inventory paths are unit-testable headlessly.

### 4.3 [S/G] Authoritative voxel edit replication
`[ ]` New nsd message `VoxelEditMessage { ulong EntityUuid; int X, Y, Z; Voxel Voxel }` broadcast by
the server to **all** clients when an edit is applied.
- Applyed by both the origin client and remote clients.
- **Acceptance:** server edit → all clients receive the delta and apply it; structure save reflects the
  edit.

---

## Phase 5 — Client prediction + reconcile (shared apply)

**Goal:** instant feel through prediction using the exact same resolve/apply code as the server.

### 5.1 [G] Refactor PlayerInteractionService to network intent
`[ ]` `PlayerInteractionService` stops mutating authority state. Instead:
- Read the (shared) equipment/inventory → produce interaction intent.
- **Predict** via the same `SharedInteractionResolver`, write a **presentation-only** local voxel edit,
  record the target in a `PendingInteractionQueue` keyed by `(entity, coordinate, sequence)`.
- Send the edge event upstream.
- Keep `PlaceEvent`/`BreakEvent` as **presentation hooks** (ghost/SFX) — never authority mutation.
- **Acceptance:** player interacts through the intent + prediction path; no direct authority
  `VoxelObject.Set` remains in interaction handlers.

### 5.2 [G] Client voxel reconcile system
`[ ]` New `ClientVoxelReconcileSystem` applies authoritative `VoxelEditMessage`s via the **same shared
apply path** as the server (`VoxelObject.Set` + `VoxelEntityBuilder.Rebuild`):
- confirm-match → no-op (already predicted);
- edit missing for a pending entry within a bound → **revert** prediction (server rejected);
- edit differs → **snap** to authority.
- Correlate by `(entity, coordinate, sequence)`.
- Wire into `ClientReconcileSystem`'s gate (apply only once `Playing`).
- **Acceptance:** round-trip works with and without prediction; no drift under rapid/rapidly-rejected
  edits.

---

## Phase 6 — Server modding API (the payoff)

### 6.1 [G] Expose the interaction resolution hook
`[ ]` Wrap `ServerInteractionSystem`'s resolution as a **public, server-side mod API**: mods register
handlers keyed on `(interaction kind, held item, game mode, cell context)` that run after base
validation and may reject/augment/override the action — entirely server-side, no client mod.
- **Acceptance:** a server-only mod can add/customize an interaction without any client-side change;
  documented example in-tree.

---

## Phase 7 — Docs, tests, and validation

### 7.1 [G] Documentation
`[ ]` Update `NETWORKING.md` (and `AGENTS.md` networking section) for: interaction context components,
`CharacterSeed` join sync, `InputComponent` held-state, `InteractionEvent`, `VoxelEditMessage`,
`SharedInteractionResolver`, the new systems, and the modding hook.
Keep the transport assumption (ordered/lossless) documented.

### 7.2 [G] Integration tests
`[ ]` Headless, cross-platform (no window; Linux-safe) over `LocalConnection`:
- Resolver parity (Phase 4.1).
- Interaction edge staging/losslessness/dedupe (Phase 3).
- Server voxel edit → broadcast → client apply (+ reveal reconcile).
- Full join: seed character context → remote/late witness full state.
- Survival vs creative consumption.
- Collider rebuild after edit is observable server-side.
- TCP peer smoke once `TcpTransport` demux (LOCAL-SERVER-SINGLEPLAYER 5.2) lands.

### 7.3 [G] Live smoke (Windows/Reef)
`[ ]` Two in-process clients: one edits voxels; the other visibly observes the same edits; rapid
rejected-edit recovery has no stuck ghosts.

---

## Risk register

| Risk | Mitigation |
|---|---|
| Client prediction and server authority resolve **different cells** (fiddly screen/reach-around math) | Port target-selection to **shared code** used by both (decision 5, 4.1); resolver parity tests gate it |
| Voxel coordinator parity drifts between client/server | Move `VoxelObject` + parity tests to shared once (Phase 1), never forked |
| Stale structure collider after edit → future raycasts wrong | Server rebuilds `ColliderComponent` after every edit (4.2) |
| Context (inventory/equipment/mode) not actually server-owned → interactions still client-decideable | Full server-thin-client context seeded at join (decision 2, 2.3); server-owned components replicate |
| Editing the same voxel from two clients (a race) | Server is sole author; edits serialized on the server tick; order = server apply order; broadcast covers all |
| Prediction shows a ghost the server later rejects | Pending queue + bounded revert (5.2); share the resolver so rejection is the *rare* case |
| Vulnerability/anti-cheat (client claims place/break, fakes loot) | Server client-hint + validate; server owns inventory counts; client hint never trusted (decision 5) |
| Voxel-edit replication volume | Delta message per edit (not full re-stream); transport ordered (decision 8) |
| Component migration breaks UI/inventory code | Migrate to shared as single source; UI reads migrated types (decision 3, 2.1); gate on build |
| Save backbone diverges (edits not persisted) | Re-derive `VoxelEntityDataComponent.Chunks` from shared `VoxelObject` at save (decision 6, 4.2) |

---

## Working rules — commit boundaries & engine policy

- **Engine set**: everything except `WaywardBeyond.*` (`Swordfish`, `Swordfish.ECS`,
  `Swordfish.Library`, `Swordfish.Integrations`, `Swordfish.Compilation`, `Shoal`, `Reef`,
  `Shoal.Extensions.Swordfish`, launchers, demo/editor). **Game set**: `WaywardBeyond.*`.
- **Task labels**: `[E]` engine · `[S]` shared/game-helper · `[G]` game.
- **Commit rule**: an `[E]` change is its own engine commit, committed **before** the game commits that
  consume it, and must build standalone. A mixed engine+game commit is never allowed.
- **Last-resort rule**: game tasks must not hack around the engine, and must not request engine changes
  without generic justification; game-specific needs stay in game code.
- **Expectation**: this plan is **game/shared-only**. If any `[E]` engine change becomes necessary, it
  must land first as a standalone commit and be recorded here with justification.