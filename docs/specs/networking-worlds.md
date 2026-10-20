# Networking — Multiple Server Worlds

One subject: the server serves N isolated per-level worlds with idle unload.
The server world graph is fully DI driven: systems and services register as
templates, each world resolves and pins its own instances, and any Shoal module
can add world systems. A second client joining a different level does not tear
down the first world.

## World lifecycle

1. `ServerHostCommands` runs on the server thread. It receives a routed join
   from `ServerHandlers`, calls `ServerWorldHost.RouteJoin`, and the host creates
   or finds the world for the requested level.
2. `GetOrCreate(levelGuid)` loads the authoritative world once into a fresh
   world container, then binds the peer to that world. Level load happens before
   the join reaches the world, so a join can never unload under itself.
3. The world's `ServerJoinSystem` seats the player: spawn, mirror entity,
   session, and the `JoinAccept` result. The world stream is a separate
   `StreamLevelAsync` call.
4. Every server tick the host ticks each live world in creation order through
   the engine `World`, with a per-world sim counter owned by each world's
   `SimulationStep`.
5. A world whose `SessionManager.Count == 0` for `Server.IdleUnloadMs`
   (`NetworkingConfig.Server`, default 60000) unloads: final flush via the
   world's `LevelSaveService.QueueSave` (captured on the server thread,
   persisted in the background), peer bindings cleared, world container
   disposed.
6. A late join recreates the world before any other processing of that peer.
7. A menu-time delete runs on the server thread. `ServerWorldHost.DeleteLevel`
   disposes the loaded world without a final save, then removes the level from
   the `ILevelCatalog`. The write is skipped because it would recreate the
   removed files.

## DI: per-world graphs from module templates

The server module registers the shared host wire-up (`HostComposition`) and
every per-world service and system as a transient template in the root
container (`ServerComposition.cs`). `ServerWorld` resolves each template once and
pins it into an exclusive child container per world
(`ContainerTools.CreateChild`, `RegistrySharing.CloneAndDropCache`,
`withDisposables: false`). Every system's constructor dependencies then resolve
to that world's own instance graph.

Shared singletons resolve through the child fall-through:
`NetworkRegistry`, codecs, `SkillDatabase`, `IBrickRegistry`,
`IInteractionContent`, `ILevelCatalog` backing, `NetworkingConfig`, the
loopback transport, and the RPC hosts. `withDisposables: false` is required: a
child-container disposal would otherwise cascade to the root's singleton
disposables.

World systems register under the `IServerWorldSystem` marker, never
`IEntitySystem`, so the client's ECS context cannot resolve them. Any module
calls `ServerComposition.RegisterServerSystem<T>()`, which appends after the
built-ins. Registration order is the world's tick order.

`Reuse.Scoped` and scoped `RegisterDelegate`s are deliberately unused. The
template plus pinned-instance design avoids DryIoc's broken scoped-factory path
on current runtimes.

## What is per-world

- `World`/`DataStore`, `SessionManager`, `LevelSaveService`, `SimulationStep`,
  physics, `NetworkReplicationSystem`, `ServerSkillSystem`, `ServerJoinQueue`,
  `IUserPermissionService`, and the interaction world factory.
- World systems, in canonical tick order:
  `ServerJoinSystem` → `ServerSaveSystem` → `NetworkApplySystem` →
  `ServerInventorySystem` → `ServerHeartbeatService` → physics →
  `ServerInteractionSystem` → `ServerChatSystem` → `NetworkPublishSystem`.
- Replication state is per world, so full-sync sets, streaming gates, pending
  snapshots, and acks cannot leak between worlds.

## What stays shared

`NetworkRegistry`, codecs, `SkillDatabase`, `IBrickRegistry`,
`IInteractionContent` and the handler registry, `NetworkingConfig`,
`LoopbackRpcServerTransport`, `ServerRpcHost`/`LanHost` (one listen socket),
`ServerPeers`, `ServerHostCommands`, `ServerHandlers`, `ILevelCatalog` and
`StoragePaths`.

## Peer routing

- `ServerRpcHost` accepts loopback and LAN connections into two `RpcServer`
  instances. It records each connection in `ServerPeers` by game `Uuid` and by
  `(IRpcHost, clientId)`.
- `ServerHandlers` only enqueues. A menu call is served by
  `ServerHostCommands` on the server thread.
- A join is routed by `ServerWorldHost.RouteJoin`. The peer gains a world
  binding, and the join enters that world's `ServerJoinQueue`.
- A leave enters the world's leave queue. Leaves run before joins in the world
  tick, so a menu return and a rejoin in the same tick tear down the old session
  first.
- A peer disconnect is queued by `ServerRpcHost`. The host unbinds the world on
  the server thread and enqueues the session teardown in that world.
- A peer with no world is a menu peer. `PendingConnectionHeartbeat` keeps it
  alive. Its next join routes to the world it names.

## Menu-time calls

`ServerLevelManager` serves `ListLevels` and `CreateLevel` on the server thread.
`ServerWorldHost.DeleteLevel` serves `DeleteLevel`. Calls are correlated by
nsd-rpc, so no per-connection FIFO chain exists. `ILevelCatalog` access stays
single-threaded.

## Persistence and failure modes

`LevelSaveService` is per world and owns the per-level save database. A world's
load failure is handled in its own tick: a failed `LoadLevel` leaves a fresh
empty world, the join continues at the default spawn, and other worlds are
untouched. A mid-stream unload cannot occur because joins create the world
first and the idle window covers load. A forced delete never queues a final
save.

## Threading and determinism

All worlds tick sequentially on the single server thread, so the shared step's
per-world sim tick counts stay independent and deterministic. Each world gets
one fixed step per server tick. A slow world delays the following worlds within
the same tick.

## Source of truth

- `WaywardBeyond.Server/ServerWorldHost.cs`
- `WaywardBeyond.Server/ServerWorld.cs`
- `WaywardBeyond.Server/{ServerRpcHost,ServerPeers,ServerPeer,ServerHostCommands}.cs`
- `WaywardBeyond.Server/ServerModule.cs`
- `WaywardBeyond.Server/ServerLevelManager.cs`
- `WaywardBeyond.Server/ServerComposition.cs`
- `docs/specs/networking-join.md` (join flow)
- `docs/specs/persistence.md` (level save data)
