# Game Networking Architecture

This document describes the networking architecture for the game: a message-based, ECS-driven
client-server model where **singleplayer is a local server in the same process**. Detailed task
tracking for the current initiative lives in [`LOCAL-SERVER-SINGLEPLAYER.md`](./LOCAL-SERVER-SINGLEPLAYER.md).

## Overview

The game is architected as a multiplayer-ready client-server game from the start. The `WaywardBeyond`
client and server are separate ECS `World`s that communicate **only** through serialized nsd messages
over a transport abstraction. Singleplayer runs the authoritative server **in-process** on its own
thread and connects to it over a `LocalConnection` loopback that exercises the full wire protocol —
there is no separate singleplayer simulation path.

### Core principles

- **Singleplayer = local server.** One binary, two worlds, one real message boundary between them.
  A bug fixed on the server is fixed in both singleplayer and multiplayer by construction.
- **Same code, client and server.** Gameplay simulation is shared code registered on both worlds;
  only the transport and the systems that sample local input differ.
- **Authoritative server.** The server's ECS is the source of truth for world state; clients predict
  and reconcile.
- **Message-based transport.** Both sides speak serialized nsd messages. No shared `DataStore`, no
  direct object access, no `if (IsLocal)` shortcuts in the hot path — even the in-process loopback
  serializes through the wire format so the transport can be swapped without behavioral change.
- **Delta replication.** Only dirty networked components are serialized and sent each tick, driven by
  ECS dirty tracking.
- **Stable wire identity.** Components are registered with a stable `Uuid` and a `NetworkDirection`.

## Current state summary

| Area | Status |
|---|---|
| Separate client/server worlds on separate threads | Implemented |
| Serialized loopback (`LocalConnection`) with full wire format | Implemented |
| Dirty-driven replication (server → client) | Implemented, but server never simulates anything |
| Client-owned input replication (client → server) | Implemented |
| Server spawn handshake | Implemented (single-client assumptions) |
| Client prediction + reconciliation | Partially implemented; dormant against real data |
| Authoritative server simulation (physics/movement) | **Not implemented** — client drives its own motion |
| Multi-client sessions | Scaffolded (`SessionManager`), not wired |
| Peer transport (`TcpTransport`) | Exists, unexercised, no per-type demux |

See [Current gaps](#current-gaps--known-issues) for the full list.

## Worlds, threads, and the process boundary

Two independent ECS worlds run concurrently inside the game process:

- **Client world** — the engine's `ECSContext` (`Swordfish/ECS/ECSContext.cs`), ticked on the `"ECS"`
  thread. Runs engine systems (rendering, physics) plus client gameplay systems.
- **Server world** — `WaywardBeyond.Server.Core/ServerContext.cs`, ticked on the `"Server"` thread.
  Runs the authoritative server systems.

`ServerContext` builds its own `World`/`DataStore` and is host-agnostic: it takes an
`IServerConnection` and an `ILoggerFactory`, constructs its systems, and tick them. Its doc comment
notes that in a networked layout it would run standalone; a future dedicated server is a thin host
around this same class.

The server is currently hosted inside the client app: `Client.Core/Injector.cs` calls
`ServerComposition.Register(container)` (line 143), which registers `ServerContext` as an
`IEntryPoint`. This hard-wired wiring exists because `WaywardBeyond.Server.Core` has no
`manifest.toml` yet; it is listed in `modules.toml` but skipped by the module loader.

The only coupling between the two sides is the transport. Server and client worlds never touch each
other's `DataStore`; they exchange `ComponentSnapshot` payloads.

## Messages and serialization

Wire messages are nsd schemas (`WaywardBeyond.Shared.Networking/CodeGen/network.nsd`) compiled by
`nsdc` into structs with generated `Serialize()`/`Deserialize(ReadOnlySpan<byte>)`:

```nsd
message ComponentSnapshot
{
    ulong Entity    = 0;
    ulong TypeUuid  = 1;
    byte[] Payload  = 2;
}

message WorldSnapshot
{
    uint TickNumber           = 0;
    uint LastProcessedInput   = 1;
    ComponentSnapshot[] Components     = 2;
    ulong[] RemovedEntities   = 3;
}

message SpawnRequest
{
    ulong CharacterId   = 0;
}

message SpawnResponse
{
    ulong Entity    = 0;
    bool  Accepted  = 1;
}

message TransformMessage { /* position, orientation, scale */ }
message PhysicsMessage  { /* velocity, torque */ }
```

`InputComponent` (`CodeGen/components.nsd`) is a networked component and an nsd message:

```nsd
message InputComponent
{
    float MovementX          = 0;
    float MovementY          = 1;
    float MovementZ          = 2;
    float LookDeltaX         = 3;
    float LookDeltaY         = 4;
    bool  Jump               = 5;
    uint  SequenceNumber     = 6;
    uint  ServerTickAtSample = 7;
}
```

`NsdMessageSerializer<T>` (`Serialization/NsdMessageSerializer.cs`) adapts any nsd message to the
generic `ISerializer<T>` used by the transports, by reflection-driving the generated methods. A
`SerializerCache` indexes the DI-provided serializers by message type.

> **No envelope.** The wire is plain serialized nsd messages with a raw transports-only framing
> (TCP uses a 4-byte length prefix). There is **no** `GamePacket` envelope, no `IDataSender`/
> `IDataReceiver` byte pipeline, no per-message sequence/ack/RTT layer. Reliability/ordering is left
> to the transport (TCP today). The `SequenceNumber`/`LastAckedInput`/`LastAckedSnapshot` fields are
> **gameplay-level prediction acks**, not transport reliability — see [Prediction
> acks](#prediction-acks).

## `NetworkRegistry` — stable component identity

`WaywardBeyond.Shared.Networking/Registry/NetworkRegistry.cs` maps networked component types to a
stable wire identity:

- `Initialize(assemblies)` scans assemblies for value-type `IDataComponent` structs annotated with
  `[NetworkComponent(uuid, direction)]` and registers each with an `NsdComponentCodec<T>` that drives
  the generated nsd serializers.
- `Register<T>(Uuid, NetworkDirection, IPayloadCodec)` is the explicit path for engine/third-party
  components that are not nsd messages.
- Reverse lookups: `TryGetInfo(Type)` / `TryGetInfo(Uuid)`; enumeration by direction via
  `GetComponents(NetworkDirection)`.

Registered entries (wired in `Client.Core/Injector.cs:123-125`):

| Component | Uuid | Direction | Codec |
|---|---|---|---|
| `InputComponent` | 1 (attribute) | ClientOwned | `NsdComponentCodec<InputComponent>` |
| `TransformComponent` | 2 | ServerOwned | `TransformCodec` (hand-written nsd adapter) |
| `PhysicsComponent` | 3 | ServerOwned | `PhysicsCodec` (hand-written nsd adapter) |

### `NetworkDirection`

- `ServerOwned` — the server is authoritative; replicated downstream to clients.
- `ClientOwned` — the client is authoritative; replicated upstream to the server (e.g. input).

### Codecs

`IPayloadCodec` serializes a component into the opaque `byte[]` payload of a `ComponentSnapshot` and
applies a payload back onto an entity. The default `NsdComponentCodec<T>` requires the component to
be an nsd message (piecewise `InputComponent` provides this); `TransformCodec`/`PhysicsCodec`
are hand-written adapters (see `Client.Core/Networking/`).

## Transport abstraction

Two marker interfaces split `INetworkTransport` into its two roles:

```csharp
public interface INetworkTransport
{
    bool IsConnected { get; }
    bool IsLocal { get; }
    Result Send<T>(in T message);
    Result<T> Receive<T>();
}

public interface IClientConnection : INetworkTransport { } // upstream sends, downstream receives
public interface IServerConnection : INetworkTransport { } // downstream sends, upstream receives
```

`IsLocal` is surfaced via `GameClient` but is **not consulted in the production hot path** — client
and server systems uniformly `Send`/`Receive` typed messages regardless of transport.

### `LocalConnection` (singleplayer / in-process)

`Transport/LocalConnection.cs` creates a client endpoint and a server endpoint backed by per-type
`ConcurrentQueue<byte[]>`s. Every `Send<T>` serializes through the wire format and enqueues the
resulting bytes; `Receive<T>` dequeues and deserializes. This exercises the full protocol — same
serializers, same message shapes, same polling model as a real socket — with zero network I/O.

### `TcpTransport` (peer / LAN / dedicated)

`Transport/TcpTransport.cs` is a single-threaded TCP peer (`Connect(host, port)` for client mode,
`Listen(port)` for server mode). It length-prefixes each serialized message and reads frames on a
background thread into a **single shared receive queue** — it has no per-type demux yet, so it cannot
yet host the polling systems that each `Receive<T>` a distinct type. It is unexercised in production
code and is the first real transport to plug in once demux lands.

## Spawn handshake

1. `ClientPlayerSpawnSystem` (`Client.Core/Systems/`) submits a `SpawnRequest` with a character ID.
2. `ServerSpawnSystem` (`Server.Core/Systems/`) allocates a server entity, adds a `NetworkComponent`,
   records ownership, and replies `SpawnResponse { Entity, Accepted }`.
3. The client allocates an entity with the same `Uuid`, decorates it, and sends its initial
   `TransformComponent` up as a placement.
4. `NetworkReplicationSystem.ApplyPlacement` seats it on the server mirror and clears its dirty flag.

Today ownership is tracked by `ServerPlayerOwnership`, a **single** `Uuid?` — a single-client
assumption. The server also skips echoing the owned player's transform back to its owner
(`NetworkReplicationSystem.OnTickAction`, lines ~181-186), because local client motion is
self-authoritative. Both of these are scheduled for removal (see
[`LOCAL-SERVER-SINGLEPLAYER.md`](./LOCAL-SERVER-SINGLEPLAYER.md)).

## Replication (dirty-driven)

### Server → client

`NetworkReplicationSystem` (`Server.Core/Systems/`):

- `ApplyInbound` drains `WorldSnapshot`s from clients, applying `ClientOwned` components (materializing
  a server mirror for unknown entity uuids) and advancing `LastAckedInput`/`LastAckedSnapshot` on
  `InputComponent` application.
- Each tick, it queries entities carrying `NetworkComponent`, serializes every **dirty** `ServerOwned`
  component into `ComponentSnapshot`s, collects **removed** entities via `QueryRemoved`, and sends one
  `WorldSnapshot { TickNumber, LastProcessedInput, Components, RemovedEntities }`.

### Client → server

`ClientReplicationSystem` (`Client.Core/Systems/`) iterates **dirty `ClientOwned`** components — today
only `InputComponent` — and sends them up in a `WorldSnapshot`.

## Client prediction & reconciliation

- `ClientInputSystem` samples `IInputService`, builds an `InputComponent` (with a monotonic
  `SequenceNumber` and `ServerTickAtSample` echoing the last applied snapshot tick), writes it to the
  local player, and stores a copy in `PendingInputComponent` (a 256-entry ring buffer).
- `PlayerControllerSystem` currently drives the client's own motion from that input — local
  client motion is authoritative. **This is the client-authoritative behavior being replaced.**
- `ClientReconcileSystem` receives server `WorldSnapshot`s, applies `ServerOwned` components, frees
  despawned entities, trims `PendingInputComponent` by `LastProcessedInput`, and re-applies the
  surviving inputs. `SnapshotAckTracker` records the last applied snapshot tick.

Because the server does not simulate anything today, no authoritative state flows back for the owned
player and reconciliation is structurally present but inert.

### Prediction acks

- **Input ack** — `NetworkComponent.LastAckedInput` (advanced by the server, carried downstream in
  `WorldSnapshot.LastProcessedInput`) trims the client's prediction history.
- **Snapshot ack** — `SnapshotAckTracker`/`InputComponent.ServerTickAtSample` ties sampled input to the
  server tick it was sampled against.

These are gameplay-level bookkeeping for prediction. They are **not** a transport reliability layer.

## Current gaps / known issues

- **No server-side simulation.** The server mirror stores what the client sends; no physics or
  movement systems run on `ServerContext.World`.
- **Client-authoritative owned player.** `PlayerControllerSystem` (client-only) authors motion; the
  server's transform-echo skip assumes it.
- **Single-client coupling.** `ServerPlayerOwnership` holds one uuid; `SessionManager` exists but is
  dormant.
- **No disconnect handling.** Remote/client mirrors are not cleaned up; sessions never end.
- **`TcpTransport` has a single shared receive queue** — no per-type demux; unusable for the current
  polling model and unexercised.
- **Server boot is hard-wired.** `ServerComposition.Register` is invoked from the client's `Injector`
  because `WaywardBeyond.Server.Core` lacks a `manifest.toml`.
- **Input look is raw cursor delta** (`LookDeltaX/Y`), with sensitivity applied at consumption.

## Direction

The current initiative targets:

1. Server-authoritative simulation (physics + movement) on the server world, with client prediction
   and reconciliation driven **per physics step**: the solver already fixed-steps at 0.016s
   (`JoltPhysicsSystem`), and tick-tagged input commands are applied exactly once per step so client
   and server integrate identically regardless of tick-thread pace. The tag clock is a canonical
   **sim tick = physics-step ordinal** (`TickNumber`/`LastProcessedInput`/`ServerTickAtSample` are
   rebased onto it), not the server's per-world replication tick.
2. **Kinematic look**: the client sends sensitivity-resolved absolute yaw/pitch; the server applies it
   directly. Mouse sensitivity stays a purely local player setting and is never networked.
3. Server-side voxel world with **replicated world-body dynamics** (structures remain Dynamic and
   server-authored; motion replicates via the snapshot path) plus remote-player visuals relaying only a
   minimal public character view (identity/name/appearance — never inventory or attributes).
4. Multi-client routing via a **server-side connection hub** with **per-session acks** (the single
   `WorldSnapshot.LastProcessedInput` field is per-recipient; the server composes a per-client
   snapshot per tick), then **server-owned world state**: the server owns the world save (`levels`
   KV), world generation, and per-character location persistence, streaming the full world to clients
   at join; clients own only `characters` KV.
5. Per-type transport demux and transport selection as a DI decision; a dedicated executable stays a
   clean seam (server boot via module discovery), not a shipped launcher.

The plan is tracked in detail in [`LOCAL-SERVER-SINGLEPLAYER.md`](./LOCAL-SERVER-SINGLEPLAYER.md).

## Persistence (adjacent, not transport)

All save data — client-owned `characters` and server-owned `levels` — persists through a local NATS
JetStream server launched by `PersistentNatsProcess` (`WaywardBeyond.Server.Core/Streaming/`,
started from the client `Entry`), wrapped by `KeyValueStore` (NATS KV) in `WaywardBeyond.Shared.Data`.
This is separate from the networking transport but central to the state-ownership / join-streaming
work in [`LOCAL-SERVER-SINGLEPLAYER.md`](./LOCAL-SERVER-SINGLEPLAYER.md).

## Legacy / unrelated server code

`WaywardBeyond.Server.Core` also contains pre-dating infrastructure that is **not** part of this
networking model: `PacketStreamClient`/`PacketNatsSerializer`/`IProtocol`/`ProtocolV1`
(Torches-era inter-server packet streaming, `CodeGen/Output/Packet.cs` and friends — not wired into the
current transport path).