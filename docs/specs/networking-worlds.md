# Networking — Multiple Server Worlds

One subject: the server serves N isolated per-level worlds with idle unload.
The server world graph is fully DI driven: systems and services register as
templates, each world resolves and pins its own instances, and any Shoal
module can add world systems. A second client joining a different level no
longer tears down the first world.

## World lifecycle

1. `ServerWorldHost` (`WaywardBeyond.Server/ServerWorldHost.cs`, `IEntryPoint`)
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
5. A world whose `SessionManager.Count == 0` for `Server.IdleUnloadMs`
   (`NetworkingConfig.Server`, default 60000) unloads: final flush via the world's
   `LevelSaveService.QueueSave` (captured on the server thread, persisted in
   the background), connections unbound back into `PendingJoins`, world
   container disposed.
6. A late join recreates the world before any other processing of that client.
7. A menu-time delete requests the world be torn down: `ServerWorldHost`
   drains `PendingLevelDeletes` on the server thread, disposes the loaded
   world without a final save, and the `ILevelCatalog` removes its files.

## DI: per-world graphs from module templates

The server module (`WaywardBeyond.Server/ServerComposition.cs`) registers
every per-world service and system as a **transient template** in the root
container. `ServerWorld` (`ServerWorld.cs`) resolves each template once and
pins it into an **exclusive child container** per world
(`ContainerTools.CreateChild`, `RegistrySharing.CloneAndDropCache`,
`withDisposables: false`); every system's constructor dependencies then resolve
to that world's own instance graph. Shared singletons (registry, codecs,
`SkillDatabase`, `IBrickIdMap`, `IInteractionContent`, the `ILevelCatalog`
backing, `NetworkingConfig`, `TcpServerHost`, `LocalConnection`) resolve
through the child fall-through.
`withDisposables: false` is required: a child-container disposal cascades to
the root's singleton disposables otherwise, so unloading a world would dispose
the server host, the LAN host, and every other root singleton. The world's
pinned instances (registered into the child) still dispose on world unload.

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
  `LevelSaveService`, `SharedSimulationStep`, `JoltPhysicsSystem`,
  `NetworkReplicationSystem`, `ServerSkillSystem`, `ServerJoinQueue`,
  `IUserPermissionService`, the interaction world factory.
- World systems, in canonical tick order:
  `ServerJoinSystem` → `ServerWorldSystem` → `NetworkApplySystem` →
  `ServerInventorySystem` → `ServerHeartbeatService` → `ServerPhysicsSystem` →
  `ServerInteractionSystem` → `ServerChatSystem` → `NetworkPublishSystem`.
  The replication system's apply and publish stages split into two ordered
  systems so physics and the shared motion step run between them
  (`NetworkApplySystem.cs`, `NetworkPublishSystem.cs`). The per-world
  `ServerWorldSystem` runs the autosave cadence, authorizes in-world save
  requests, and broadcasts the save notifications. See
  [permissions](permissions.md).
- **Server-level** (not per-world): `ServerLevelManager` serves the menu-time
  create/list/delete requests on connections that have not joined a world yet;
  the host ticks it before world routing. Deletes defer to
  `PendingLevelDeletes` so the host can tear down a loaded world first. The
  client pulls the listing while the save-select page is visible, so a level
  another client creates appears without a reconnect.
- Replication state: `NetworkReplicationSystem` is per world, so full-sync
  sets, streaming gates, pending snapshots, and acks cannot leak between
  worlds. Publish reads the world's sim tick from the injected
  `SharedSimulationStep` (no host wiring).

## What stays shared

`NetworkRegistry`, codecs/serializers, `SkillDatabase`, `IBrickIdMap`,
`IInteractionContent` + handler registry, `NetworkingConfig`,
`TcpServerHost`/`LanHost` (one listen socket), `LocalConnection`,
`ILevelCatalog` + `StoragePaths` (the level save databases),
`PendingLevelDeletes`.

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

When a player leaves to the menu, the host returns the connection to the
pending set after the world tick (`ServerWorldHost.ReturnSessionlessConnectionsToPending`, `ServerWorldHost.cs:89`).
A bound connection without a session is idle, never mid-join: joins are
enqueued and registered within one world tick. The next `JoinRequest` is then
routed, so a player can switch saves. A world serves exactly one level;
`ServerJoinSystem.HandleJoin` refuses a join that names another level with an
error log (`ServerJoinSystem.cs`).

The singleplayer host is the N=1 case: its loopback rides the same pending →
hub → join path when it joins a world.

## Persistence and failure modes

`LevelSaveService` is per world (its per-level state such as
`CurrentLevelGuid` cannot be shared) over the shared level catalog. Character
location resolves against the joining world. A world's load failure is handled
in its own tick: a failed `LoadLevel` leaves a fresh empty world, the join
continues at the default spawn, and other worlds are untouched. A mid-stream
unload cannot occur: joins atomically (re)create the world first, and the idle
window covers load. A forced delete never queues a final save: the write would
recreate the removed files.

## Threading and determinism

All worlds tick sequentially on the single server thread, so the shared
step's per-world sim tick counts stay independent and deterministic. Fairness:
each world gets one fixed step per server tick; a slow world delays the
following worlds within the same tick (documented; re-visit only under
measured pressure from scaling work).

## Test strategy

- `Swordfish.Tests/ChatTests.cs` exercises multi-client routing over the
  `ServerConnectionHub`.
- `Swordfish.Tests/TcpServerHostTests.cs` exercises socket accept, independent
  per-peer routing, and disconnect detection.
- `Swordfish.Tests/RejoinConcurrencyTests.cs` drives the client and server world
  teardown and rebuild concurrently.
  (pending-set pruning), and the full networking suite stay green.

## Source of truth

- `WaywardBeyond.Server/ServerWorldHost.cs`
- `WaywardBeyond.Server/ServerWorld.cs`
- `WaywardBeyond.Server/ServerLevelManager.cs`
- `WaywardBeyond.Server/ServerComposition.cs`
- `docs/specs/networking-join.md` (world-routed joins)
- `docs/specs/persistence.md` (level save data)