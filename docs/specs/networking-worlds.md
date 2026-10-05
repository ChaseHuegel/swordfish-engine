# Networking — Multiple Server Worlds (proposal)

One subject: the design for serving N isolated per-level server worlds with
idle unload. This is a development proposal, not an implementation. It
resolves how a second client joining a different level no longer tears down
the first client's world.

## Problem recap

The server hosts exactly one global loaded level. `ServerJoinSystem.HandleJoin`
unconditionally loads the joining client's `LevelGuid`, so a second client
joining a different level frees the world under the first client's session.
Per-client worlds are the locked scope: each loaded level and its players must
be isolated with no cross-pollination, and a world with no connected clients
unloads.

## Decisions

| Trade-off | Decision | Rationale |
|---|---|---|
| Hub topology | One `ServerConnectionHub` per world | Routing is the world's own concern; the join system dispatches each client to its world's hub at session creation. No shared-hub cross-world demux. |
| World runtime | One `ServerContext`-equivalent instance per world, ticked sequentially on one server thread | Preserves determinism and the fixed stage order; no physics-concurrency changes. |
| DI boundry | Per-world `DataStore`, `JoltPhysicsSystem`, `SharedSimulationStep`, hub, session binding, replication state. Registry, codecs, serializers, SkillDatabase, brick map, `WorldSaveService` stay shared singletons | The shared set is stateless or content; the per-world set is all state. |
| DI mechanism | Named registrations resolved through a per-world composition scope (`container.CreateChildContainer` per world), NOT decorators | Child containers give a clean per-world object graph without touching engine registrations. |
| Unload trigger | A world unloads when `hub.Count == 0` for `WorldIdleUnloadMs` (new `NetworkingSettings` key, default 60000) with the last disconnect's teardown already streamed | Guards against unloading mid-join/stream: joins atomically (re)create the world first, and the idle window covers load. |
| Unload handoff | Final authoritative flush via `WorldSaveService.QueueWorldSave`, dispose physics bodies (existing `PhysicsComponent.Dispose` path), dispose the world child container after the tick's publish | The flush runs on the server thread before container disposal. |
| Singleplayer | The host maps to a single world whose hub also seeds the in-process `LocalConnection` loopback | The N=1 case of the same model. |
| Persistence | Per-world flush + `EndSessionStamp` per its own session teardown; character location resolves against the joining world | No change to the bucket model. |
| Networking | Sessions bind to a world at join; per-world publish + ack state; interaction and chat iterate only their world's hub; disconnect teardown only touches the owning world | Despawns and acks cannot leak across worlds. |
| Failure modes | A world's load failure is handled in its own tick: the joining client gets the failed response, other worlds continue; a mid-stream unload cannot occur (unload waits for zero clients + idle window) | Isolation of errors. |

## World lifecycle

1. `ServerWorldHost` (new system, owns the world table) receives a join
   request.
2. `GetOrCreate(levelGuid)`: loads the authoritative world once
   (`WorldSaveService`), creates the child container, resolves the world's
   `ServerContext`-equivalent, starts its sim tick.
3. Join binds the client: session → world, hub add, spawn, stream (existing
   `ServerJoinSystem` flow, now against the world's hub).
4. Every server tick: the host ticks each live world (`ApplyStage →
   inventory → heartbeats → physics → interaction → chat → PublishStage`)
   in creation order with a per-world sim counter.
5. A world with `hub.Count == 0` for the idle window unloads: flush, dispose
   physics bodies, dispose the world container, free the entry.
6. A late join recreates the world before any other processing of that
   client.

## What must become per-world

- `DataStore`, `JoltPhysicsSystem` (+ gravity config), `SharedSimulationStep`
- `ServerConnectionHub`, `SessionManager` binding (client → world → entity)
- Replication state (`NetworkReplicationSystem` instance, full-sync sets,
  streaming gates), interaction/skill/chat systems
- Server heartbeat state (per-world TPS/lag counters)

## What stays shared

`NetworkRegistry`, codecs/serializers, `SkillDatabase`, `IBrickIdMap`,
`IInteractionContent`/handler registry, `WorldSaveService` (KV-backed),
`NetworkingSettings`, `TcpServerHost`/acceptor (one listen socket; the host
routes the accepted peer to the joining world's hub after `JoinRequest`).

## Threading and determinism

All worlds tick sequentially on the single server thread, so the shared step's
per-world sim tick counts stay independent and deterministic. Fairness: each
world gets one fixed step per server tick; a slow world delays the following
worlds within the same tick (documented; re-visit only under measured pressure
from #0019's scaling work).

## Test strategy

- Multi-world join/depart: clients join levels A and B; both play
  simultaneously; B's disconnect unloads only B; A persists.
- Isolation: cross-world injection attempt (a client of A addresses B's
  entities) applies nothing and allocates nothing.
- Unload-on-idle: after the last client of a world leaves, the world flushes
  and frees; a rejoin reloads and resumes.
- Regression: `SessionRoutingTests` (same-level multi-client) and the full
  networking suite stay green.

## Source of truth (future)

- `WaywardBeyond.Server.Core/ServerWorldHost.cs` (new)
- `WaywardBeyond.Server.Core/ServerContext.cs` (per-world instance)
- `docs/specs/networking-join.md` (world-routed joins)

This proposal needs user approval before implementation (issue #0008).