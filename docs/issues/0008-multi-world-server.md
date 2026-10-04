# Research: Multiple ECS worlds — isolated per-level server worlds with idle unload

- Type: research
- Status: open
- Workflow: ../specs/issues.md

## Problem

The server currently hosts exactly one global loaded level.
`ServerJoinSystem.HandleJoin` (`ServerJoinSystem.cs:114-115`)
unconditionally calls `LoadLevel` for the joining client's `LevelGuid`, so
a second client joining a different level unloads the world under the
first client's session (structures freed, physics bodies disposed) and
play continues against a torn-down world. Same-level multi-client joins
are pinned by tests; cross-level joins are not.

Per-client worlds are in scope (decision locked): each loaded game
save/level and the players within it should be entirely isolated with no
cross-pollination of state or networking data, and a world with no
connected clients should unload to save resources and compute.

This is a large structural change. The current architecture binds the
server world to a single `ServerContext` ticked on one thread, with
`IEntitySystem`s and supporting services registered in DI as singletons
(see module wiring, e.g. `Client.Core/Injector.cs` and
`Server.Core/ServerComposition.cs`). Serving N worlds means N world
instances, N sets of per-world systems, per-world physics state, per-world
sessions/hubs, and an unload lifecycle - all within the existing DI and
module framework.

The deliverable of this issue is a thorough development proposal, not an
implementation.

## Scope to research and answer in the proposal

- **World lifecycle.** Creation, load, and teardown of a server world per
  (level, client group); where worlds are created and destroyed in the
  server tick; ordering vs the existing `ServerContext.Update` stage
  sequence; how singleplayer (host) maps onto the multi-world model.
- **Unload policy.** Definition of "no connected clients", the unload
  handoff (final flush of the authoritative save via `WorldSaveService`,
  physics disposal), and the guard against unloading a world mid-join or
  mid-stream.
- **DI changes.** The current singleton registrations for worlds and
  systems (`IECSContext`, `IEntitySystem`, `ServerConnectionHub`,
  `SessionManager`, shared sim step, physics) and what must become
  per-world instances; the DryIoc mechanisms available (decorators,
  scopes/instances, named registrations) that fit the existing module
  layout without destabilizing the client world.
- **Isolation boundaries.** What state can never be shared across worlds:
  store, physics, shared sim step, staged inputs, sessions, hubs,
  replication publish state; and what stays shared (registry, codecs,
  serializers, SKills DB, brick map).
- **Networking.** Routing joins to the correct world; whether each world
  owns a `ServerConnectionHub` or one hub dispatches by client; per-world
  `WorldSnapshot`/ack state; disconnect and despawn semantics across
  worlds; interaction and chat scoping.
- **Threading and determinism.** Ticking N worlds sequentially on the
  server thread; per-world sim-tick counters; fairness when multiple
  active worlds each want 60 Hz physics.
- **Persistence.** Per-world flush and `EndSessionStamp`; character
  location mapping to world; save-listing operations while multiple
  worlds exist.
- **Failure modes.** World load failure, join failure, mid-stream unload,
  and how each is isolated to its world.
- **Test strategy.** Multi-world join/depart cycle, isolation
  (cross-world injection attempt), unload-on-idle with persistence
  verification, regression for `SessionRoutingTests` and same-level
  joins.

## Acceptance criteria

- [ ] A development proposal document (proposed home:
      `docs/specs/networking-worlds.md`) covering every scope item above,
      with an explicit decision for each open trade-off (per-world hubs
      vs shared hub, unload trigger, DI mechanism).
- [ ] The proposal maps every current singleton dependency that must
      become per-world, with the concrete DI changes.
- [ ] The proposal is approved by the user before any implementation
      begins.
- [ ] The cross-level join behavior described above is resolved by the
      proposal's design (a second client joining a different level no
      longer tears down the first world).