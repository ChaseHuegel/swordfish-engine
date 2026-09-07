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
| Dirty-driven replication (server → client) | Implemented |
| Client-owned input replication (client → server) | Implemented |
| Server spawn handshake | Implemented |
| Client prediction + reconciliation | Implemented (server-authoritative, sim-tick driven) |
| Authoritative server simulation (physics/movement) | Implemented (shared deterministic step on the server world) |
| Multi-client sessions & disconnect | Implemented (`ServerConnectionHub` + `SessionManager`) |
| Peer transport (`TcpTransport`) | Exists, unexercised, no per-type demux (Phase 5) |

See [Current gaps](#current-gaps--known-issues) for the full list.

## Worlds, threads, and the process boundary

Two independent ECS worlds run concurrently inside the game process:

- **Client world** — the engine's `ECSContext` (`Swordfish/ECS/ECSContext.cs`), ticked on the `"ECS"`
  thread. Runs engine systems (rendering, physics) plus client gameplay systems.
- **Server world** — `WaywardBeyond.Server.Core/ServerContext.cs`, ticked on the `"Server"` thread.
  Runs the authoritative server systems.

`ServerContext` builds its own `World`/`DataStore` and is host-agnostic: it takes a
`ServerConnectionHub`, a `PhysicsSettings`, a lazy `KeyValueStore` factory, and an `ILoggerFactory`
(plus a shared `SessionManager`), constructs its systems, and ticks them. Its doc comment
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
    float LookPitch          = 3;
    float LookYaw            = 4;
    float LookRoll           = 5;
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
code and is the first real transport to plug in once demux lands (Phase 5).

### `ServerConnectionHub` (multi-client sessions)

`Transport/ServerConnectionHub.cs` aggregates one `IServerConnection` per connected client under an
opaque `Uuid` (`clientId`) assigned on `Add`. It is shared code (not server core) because the shared
transport implementations (`LocalConnection`, and later `TcpTransport`) feed it and every world side
consumes it. Server systems construct against the hub instead of a single connection:

- `Receive<T>()` polls every client connection and tags each inbound message with the `clientId` it
  arrived on — so the server's join/world systems route responses back to the requesting client and
  `SessionManager` keys a session to that client.
- `Send<T>(clientId, ...)` addresses a single client; `Clients` enumerates every connected client.
- `Remove(clientId)` queues a disconnect that `DrainDisconnects()` surfaces to the server teardown step.

Each client still owns one `IClientConnection` (upstream sends, downstream receives); only the server
side sees a hub.

### `SessionManager` & sessions

`Server.Core/SessionManager.cs` binds the full `clientId ↔ Session ↔ player entity` chain, stamping
`NetworkComponent.Session`. `ServerJoinSystem` allocates a `Session` per connection and calls
`Register`; `NetworkReplicationSystem` uses `SessionManager.TryGetEntity(clientId, ...)` to read that
client's per-entity acked input. Disconnect teardown (`ServerContext.HandleDisconnects`) disposes the
mirror's physics body, captures its uuid, frees the entity, and clears the mapping — the despawn is
then broadcast to remaining clients in `RemovedEntities`.

## Join handshake & full-world stream

The client no longer loads or generates world state. `GameSaveService`/its load stages were retired in
favor of a server-driven join that streams the entire world per entity.

1. `ClientJoinSystem` (`Client.Core/Systems/`) submits a `JoinRequest { LevelGuid, CharacterId, PublicView }`
   where `PublicView` is the minimal identity relay (`CharacterId`, `Name`, `Body`) — **never** inventory,
   attributes, or statistics. It drives `MainMenu → Loading → Playing` and is queued from the menu so view
   building runs on the client ECS thread.
2. `ServerJoinSystem` (`Server.Core/Systems/`) - via `WorldSaveService` (`Server.Core/Saves/`) - loads the
   authoritative voxel world for `LevelGuid` from the server-owned `levels` bucket (unloading any previous
   world, disposing its physics bodies), resolves the spawn transform (the persisted
   `<level>.character.<id>` location, else `Level.Spawn`), allocates the server mirror, binds it to a fresh
   `Session`, and replies `JoinAccept { Level, SpawnTransform, PlayerEntity }`.
3. It then streams the world as one `WorldEntityAdd { VoxelEntityData }` per structure (bounded per-entity,
   so no unbounded batch), followed by a `WorldStreamComplete`. The client builds a view entity for each
   arrival (mesh + a local prediction collider) on the ECS thread, and **only** transitions to `Playing` on
   `WorldStreamComplete` — which is what keeps `ClientReconcileSystem` (gated on `Playing`) inert until the
   world is fully streamed.
4. The player is seated at the server-assigned spawn; the first authoritative transform snapshot seats it
   and reconcile corrects from there. The client never authors `ServerOwned` state.

Alongside join, the save-listing menu is served by the server: `NewWorldRequest`, `ListWorldsRequest`,
`DeleteWorldRequest`, and `SaveWorldRequest` (flush authoritative world) map to `WorldSaveService`
operations, driven by a client `WorldsClient` that awaits responses the ECS thread completes. Characters
remain client-owned (`characters` bucket, unchanged).

The server owns body construction, the initial transform, and world persistence. See
[`LOCAL-SERVER-SINGLEPLAYER.md`](./LOCAL-SERVER-SINGLEPLAYER.md).

## Replication (dirty-driven)

### Server → client

`NetworkReplicationSystem` (`Server.Core/Systems/`):

- `ApplyStage` drains `WorldSnapshot`s across **all** connected clients, applying `ClientOwned`
  components (materializing a server mirror for unknown entity uuids) and advancing
  `LastAckedInput`/`LastAckedSnapshot` on `InputComponent` application. The inbound snapshot's entity
  uuid addresses the target, so cross-connection routing is unnecessary here.
- Each tick, `PublishStage` queries entities carrying `NetworkComponent`, serializes every **dirty**
  `ServerOwned` component into `ComponentSnapshot`s, then composes a **per-client** `WorldSnapshot`
  for every connected client. The component set and `RemovedEntities` are the same for all clients, but
  `LastProcessedInput` is that client's own entity's `LastAckedInput`.
- Despaws are those queued via `RequestDespawn` (which must be called before the entity is freed, since
  `DataStore.Free` clears the uuid), broadcast to remaining clients in `RemovedEntities`.
- **World/voxel bodies (Phase 2).** Server-authoritative structures (Dynamic bodies carrying a
  `NetworkComponent`) flow through this same path each tick: their `TransformComponent` and
  `PhysicsComponent` are dirtied by the physics sync cycle, so the client snaps any drift in its own
  local prediction colliders against the authoritative structure state.

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

- **No voxel content mutation replication.** World/voxel **motion** is replicated, but editing
  blocks across clients is out of scope.
- **Despawn uuids must be captured before `store.Free`.** Because `DataStore.Free` clears an entity's
  uuid, despawns are recorded explicitly (`NetworkReplicationSystem.RequestDespawn`) rather than read
  back after freeing. Wiring the disconnect path is done; world-switch despawns reuse this.
- **Disconnect detection is manual for in-process loopback.** `LocalConnection` never disconnects on its
  own; `ServerConnectionHub.Remove` is the explicit disconnect trigger. Real socket-level detection
  waits for the TCP peer path (Phase 5).
- **`TcpTransport` has a single shared receive queue** — no per-type demux; unusable for the current
  polling model and unexercised (Phase 5).
- **Server boot is hard-wired.** `ServerComposition.Register` is invoked from the client's `Injector`
  because `WaywardBeyond.Server.Core` lacks a `manifest.toml`.

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
4. **Multi-client routing** (landed): a server-side connection hub with per-session acks — the single
   `WorldSnapshot.LastProcessedInput` field is per-recipient; the server composes a per-client
   snapshot per tick. **Server-owned world state** (landed): the server owns the world save (the
   `levels` KV), world generation, and per-character location persistence, and streams the full world
   to clients per-entity at join (`WorldEntityAdd` + `WorldStreamComplete`); clients own only the
   `characters` KV and build their view world from the stream.
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