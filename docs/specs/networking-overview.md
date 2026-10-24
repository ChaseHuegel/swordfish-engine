# Networking — Overview

One subject: the shape of game networking. This doc gives the high-level model
and the process boundary. The specifics live in the sibling networking specs:

- [rpc](networking-rpc.md) — services, method ids, error codes, send policy
- [messages](networking-messages.md) — wire shapes
- [registry](networking-registry.md) — stable component identity
- [transports](networking-transports.md) — RPC transports, sessions, LAN
- [replication](networking-replication.md) — dirty-driven replication
- [prediction](networking-prediction.md) — client prediction and reconcile
- [join](networking-join.md) — join handshake and world streaming
- [voxel-edits](networking-voxel-edits.md) — server-authoritative interactions
- [inventory](networking-inventory.md) — server-authoritative inventory moves

## Model

The game is a multiplayer-ready client-server game. The `WaywardBeyond` client
and server are separate ECS `World`s. They communicate only through nsd-rpc
over a transport: each side calls typed services, and the library owns the
envelope, correlation, framing, and reliable send queue.

Singleplayer runs the authoritative server in-process on its own thread and
connects to it over the loopback. The loopback carries real RPC frames, so the
full protocol runs with no socket. There is no separate singleplayer simulation
path.

## Core principles

- **Singleplayer = local server.** One binary, two worlds, one real RPC boundary
  between them. A bug fixed on the server is fixed in both modes.
- **Same code, client and server.** Gameplay simulation is shared code
  registered on both worlds. Only the transport and the systems that sample
  local input differ.
- **Authoritative server.** The server's ECS is the source of truth for world
  state. Clients predict and reconcile.
- **RPC, not a message bus.** The game declares services and handlers. The
  library carries calls, notifications, streams, cancellation, and deadlines.
  There is no hand-written envelope, type tag, per-type receive queue, or FIFO
  request matching.
- **Reliable state.** One reliable send queue per connection. A delta snapshot
  is never dropped. A peer that cannot drain its backlog is closed and
  reconnects to a fresh state.
- **Delta replication.** Only dirty networked components are serialized and
  sent each tick, driven by ECS dirty tracking.
- **Stable wire identity.** Components are registered with a stable `Uuid` and
  a `NetworkDirection`.

## Process and thread boundary

Two independent ECS worlds run concurrently in the process:

- **Client world** — `Swordfish/ECS/ECSContext.cs`, ticked on the `"ECS"`
  thread. Runs engine systems plus client gameplay systems.
- **Server worlds** — one per loaded level, ticked sequentially on the
  `"Server"` thread by `ServerWorldHost`. Each world is its own per-world DI
  graph. See [networking-worlds](networking-worlds.md).

The server is hosted inside the client app. `WaywardBeyond.Server/ServerModule.cs`
registers the shared host wire-up (`HostComposition`), the authoritative server
composition (`ServerComposition`), `ServerWorldHost`, `ServerRpcHost`, and
`LanHost`. A dedicated server process loads the server module without the client
module. See [composition](composition.md).

The two sides never touch each other's `DataStore`. They exchange nsd payloads
through RPC handlers and notifications.

## Connection lifecycle

- The client connects the loopback at startup and sends `ClientHello`.
- A menu call is a correlated RPC call. The server thread serves it and returns
  `RpcResult<T>`.
- A join is two calls: `JoinAsync` seats the session and player mirror, then
  `StreamLevelAsync` streams the world as ordered fragments and ends with
  `LevelStreamComplete`.
- A remote connect swaps the active client to TCP and closes the loopback.
- A remote drop raises `RemoteDisconnected` for the menu teardown and restores a
  fresh loopback session. The menu can list and load local saves without a
  restart.

## Current state

| Area | Status |
|---|---|
| Separate client/server worlds on separate threads | Implemented |
| nsd-rpc services, calls, notifications, streams | Implemented |
| In-process loopback | Implemented (`LoopbackRpcServerTransport`) |
| Dirty-driven replication (server → client) | Implemented (typed snapshots, fragment batches) |
| Client-owned input replication (client → server) | Implemented |
| Server spawn handshake | Implemented (two-call join) |
| Client prediction + reconciliation | Implemented (server-authoritative, sim-tick driven) |
| Authoritative server simulation | Implemented (shared deterministic step on the server world) |
| Multi-client sessions and disconnect | Implemented (`ServerPeers` + `SessionManager`) |
| LAN server discovery (UDP beacon) | Implemented (protocol version 3) |

## Source of truth

The code is the authority.

- Implementing code: `WaywardBeyond.Networking/`, `WaywardBeyond.Server/`, and
  the client systems that consume it.

## Known gaps

- **Despawn uuids must be captured before `store.Free`.** `DataStore.Free`
  clears an entity's uuid.
- **AOI / chunked interest streaming** is out of scope. Join is a full-world
  stream.
