# Networking — Multiple Server Worlds (implementation)

One subject: the server serves N isolated per-level worlds with idle unload.
The server world graph is fully DI driven: systems and services register as
templates, each world resolves and pins its own instances, and any Shoal
module can add world systems. A second client joining a different level no
longer tears down the first world.

## World lifecycle

1. `ServerWorldHost` (`WaywardBeyond.Server.Core/ServerWorldHost.cs`, `IEntryPoint`)
   receives a join: it drains `PendingJoins` and polls each connection's
   `JoinRequest`.
2. `GetOrCreate(levelGuid)` loads the authoritative world once into a fresh
   world container, then binds the client to the world's hub. Level load
   happens before any other processing of that client, so a join can never
   unload under itself.
3. The world's `ServerJoinSystem` processes the pre-routed join: spawn, mirror
   entity, session, world stream (the existing join flow).
4. Every server tick the host ticks each live world in creation order through
   the engine `World`, with a per-world sim counter owned by each world's
   `SharedSimulationStep`.
5. A world whose `SessionManager.Count == 0` for `WorldIdleUnloadMs`
   (`NetworkingSettings`, default 60000) unloads: final flush via
   `WorldSaveService.QueueWorldSave` (captured on the server thread, persisted
   in the background), connections unbound back into `PendingJoins`, world
   container disposed.
6. A late join recreates the world before any other processing of that client.

## DI: per-world graphs from module templates

The server module (`WaywardBeyond.Server.Core/ServerComposition.cs`) registers
every per-world service and system as a **transient template** in the root
container. `ServerWorld` (`ServerWorld.cs`) resolves each template once and
pins it into an **exclusive child container** per world
(`ContainerTools.CreateChild`, `RegistrySharing.CloneAndDropCache`); every
system's constructor dependencies then resolve to that world's own instance
graph. Shared singletons (registry, codecs, `SkillDatabase`, `IBrickIdMap`,
`IInteractionContent`, `WorldSaveService`'s KV backing, `NetworkingSettings`,
`TcpServerHost`, `LocalConnection`) resolve through the child fall-through.

- World systems register under the `IServerWorldSystem` marker
  (`IServerWorldSystem.cs`), never `IEntitySystem`, so the client's ECS
  context cannot resolve them. Extensible: any module calls
  `ServerComposition.RegisterServerSystem<T>()`, which appends after the
  built-ins. Registration order is the world's tick order.
- `Reuse.Scoped` and scoped `RegisterDelegate`s are deliberately unused:
  DryIoc's compiled scoped-factory path throws `InvalidProgramException` on
  current runtimes once a second scope resolves the registration. The
  template + pinned-instance design avoids scope caching entirely.
- `ServerInteractionSystem` keeps its public convenience constructors for
  tests; the world graph pins the full constructor explicitly and the marker
  registration passes the same concrete instance through (the join system
  depends on the concrete type).
- `ServerPhysicsSystem` positions the world-scoped `JoltPhysicsSystem` in the
  tick; the engine's own registration is a root singleton the client uses.

## What is per-world

- `World`/`DataStore`, `ServerConnectionHub`, `SessionManager`,
  `WorldSaveService`, `SharedSimulationStep`, `JoltPhysicsSystem`,
  `NetworkReplicationSystem`, `ServerSkillSystem`, `ServerJoinQueue`, the
  interaction world factory.
- World systems, in canonical tick order:
  `ServerJoinSystem` → `ServerWorldSystem` → `NetworkApplySystem` →
  `ServerInventorySystem` → `ServerHeartbeatService` → `ServerPhysicsSystem` →
  `ServerInteractionSystem` → `ServerChatSystem` → `NetworkPublishSystem`.
  The replication system's apply and publish stages split into two ordered
  systems so physics and the shared motion step run between them
  (`NetworkApplySystem.cs`, `NetworkPublishSystem.cs`). The per-world
  `ServerWorldSystem` serves only the in-world save request.
- **Server-level** (not per-world): `ServerWorldManager` serves the menu-time
  create/list/delete requests on connections that have not joined a world yet;
  the host ticks it before world routing.
- Replication state: `NetworkReplicationSystem` is per world, so full-sync
  sets, streaming gates, pending snapshots, and acks cannot leak between
  worlds. Publish reads the world's sim tick from the injected
  `SharedSimulationStep` (no host wiring).

## What stays shared

`NetworkRegistry`, codecs/serializers, `SkillDatabase`, `IBrickIdMap`,
`IInteractionContent` + handler registry, `NetworkingSettings`,
`TcpServerHost`/`LanHost` (one listen socket), `LocalConnection`,
`Func<KeyValueStore>` and the NATS-backed `KeyValueStore`.

## World-routed joins

`PendingJoins` (`PendingJoins.cs`) holds accepted connections that are not yet
bound to a world: the LAN acceptor adds each peer, and the in-process host
seeds the loopback (`LocalConnection.Server` via `HostComposition.RegisterNetworking`).
The host routes each connection to the hub of the world its `JoinRequest`
names (`ServerWorldHost.RoutePendingJoins`), then hands the request to the
world through its `ServerJoinQueue`. A connection lives in `PendingJoins` or
in exactly one world hub, never both: binding migrates it out of any previous
world, and an unload returns it to the pending set. Disconnects of bound
peers release the world binding; the session teardown (despawn, stamp) stays
in the owning world's join system.

The singleplayer host is the N=1 case: its loopback rides the same pending →
hub → join path when it joins a world.

## Persistence and failure modes

`WorldSaveService` is per world (its per-level state such as
`CurrentLevelGuid` cannot be shared) over the shared KV. Character location
resolves against the joining world. A world's load failure is handled in its
own tick: a failed `LoadLevel` leaves a fresh empty world, the join continues
at the default spawn, and other worlds are untouched. A mid-stream unload
cannot occur: joins atomically (re)create the world first, and the idle
window covers load.

## Threading and determinism

All worlds tick sequentially on the single server thread, so the shared
step's per-world sim tick counts stay independent and deterministic. Fairness:
each world gets one fixed step per server tick; a slow world delays the
following worlds within the same tick (documented; re-visit only under
measured pressure from scaling work).

## Test strategy

- `Swordfish.Tests/ServerWorldCompositionTests.cs`: per-scope graph isolation,
  canonical registration order, joins delivered only to the owning world.
- `Swordfish.Tests/ServerWorldHostTests.cs`: multi-world join/depart through
  the pending route, idle unload with persistence flush, rejoin that recreates
  the world and resumes.
- Regression: `SessionRoutingTests` (same-level multi-client), `LanHostLifecycleTests`
  (pending-set pruning), and the full networking suite stay green.

## Source of truth

- `WaywardBeyond.Server.Core/ServerWorldHost.cs`
- `WaywardBeyond.Server.Core/ServerWorld.cs`
- `WaywardBeyond.Server.Core/ServerWorldManager.cs`
- `WaywardBeyond.Server.Core/ServerComposition.cs`
- `docs/specs/networking-join.md` (world-routed joins)