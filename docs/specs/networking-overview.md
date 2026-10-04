# Networking — Overview

One subject: the shape of game networking. This doc gives the high-level model
and the process boundary. The specifics live in the sibling networking specs:

- [messages](networking-messages.md) — wire shapes
- [registry](networking-registry.md) — stable component identity
- [transports](networking-transports.md) — transport, sessions, LAN
- [replication](networking-replication.md) — dirty-driven replication
- [prediction](networking-prediction.md) — client prediction and reconcile
- [join](networking-join.md) — join handshake and world streaming
- [voxel-edits](networking-voxel-edits.md) — server-authoritative interactions
- [inventory](networking-inventory.md) — server-authoritative inventory moves

## Model

The game is a multiplayer-ready client-server game. The `WaywardBeyond` client
and server are separate ECS `World`s that communicate only through serialized
nsd messages over a transport abstraction.

Singleplayer runs the authoritative server in-process on its own thread and
connects to it over a `LocalConnection` loopback. The loopback exercises the
full wire protocol. There is no separate singleplayer simulation path.

## Core principles

- **Singleplayer = local server.** One binary, two worlds, one real message
  boundary between them. A bug fixed on the server is fixed in both modes.
- **Same code, client and server.** Gameplay simulation is shared code
  registered on both worlds. Only the transport and the systems that sample
  local input differ.
- **Authoritative server.** The server's ECS is the source of truth for world
  state. Clients predict and reconcile.
- **Message-based transport.** Both sides speak serialized nsd messages. No
  shared `DataStore`, no direct object access, no `if (IsLocal)` shortcut in
  the hot path. Even the in-process loopback serializes through the wire format.
- **Delta replication.** Only dirty networked components are serialized and
  sent each tick, driven by ECS dirty tracking.
- **Stable wire identity.** Components are registered with a stable `Uuid` and
  a `NetworkDirection`.

## Process and thread boundary

Two independent ECS worlds run concurrently in the process:

- **Client world** — `Swordfish/ECS/ECSContext.cs`, ticked on the `"ECS"`
  thread. Runs engine systems plus client gameplay systems.
- **Server world** — `WaywardBeyond.Server.Core/ServerContext.cs`, ticked on
  the `"Server"` thread. Runs the authoritative server systems.

`ServerContext` builds its own `World`/`DataStore`. It is host-agnostic: it
takes a `ServerConnectionHub`, `PhysicsSettings`, a lazy `KeyValueStore`
factory, an `ILoggerFactory`, and a shared `SessionManager`.

The server is hosted inside the client app. `Server.Core/ServerModule.cs`
calls `ServerComposition.Register(container)` and registers `LanHost` unless
the process runs in `NetworkMode.Client`. The module loads through the
standard module discovery path.

The two sides never touch each other's `DataStore`. They exchange
`ComponentSnapshot` payloads on the wire.

## Current state

| Area | Status |
|---|---|
| Separate client/server worlds on separate threads | Implemented |
| Serialized loopback (`LocalConnection`) with full wire format | Implemented |
| Dirty-driven replication (server → client) | Implemented |
| Client-owned input replication (client → server) | Implemented |
| Server spawn handshake | Implemented |
| Client prediction + reconciliation | Implemented (server-authoritative, sim-tick driven) |
| Authoritative server simulation | Implemented (shared deterministic step on the server world) |
| Multi-client sessions & disconnect | Implemented (`ServerConnectionHub` + `SessionManager`) |
| Peer transport (`TcpTransport`) | Implemented (per-type demux, `TcpTransportTests`) |
| LAN server discovery (UDP beacon) | Implemented |

## Source of truth

The design and invariants were formerly tracked in root plan docs. That content
now lives split across the sibling networking specs. The code is always the
authority.

- Implementing code: `WaywardBeyond.Shared.Networking/` and the game client/server
  systems that consume it.

## Known gaps

- **Despawn uuids must be captured before `store.Free`.** `DataStore.Free`
  clears an entity's uuid.
- **In-process loopback disconnect is implicit.** The `LocalConnection` never
  raises a disconnect; remote TCP peers detect it and drive the client back to
  the menu (`ClientDisconnectSystem`).
- **AOI / chunked interest streaming** is out of scope; join is a full-world stream.