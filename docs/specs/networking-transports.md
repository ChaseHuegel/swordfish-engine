# Networking — Transports, Sessions, LAN

One subject: how messages physically move, how sessions map to clients, and
LAN discovery.

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

`IsLocal` is surfaced via `GameClient` but is **not** consulted in the
production hot path. Client and server systems uniformly `Send`/`Receive`
typed messages regardless of transport.

## `LocalConnection` (in-process)

`Transport/LocalConnection.cs` creates a client endpoint and a server endpoint
backed by per-type `ConcurrentQueue<byte[]>`s. Every `Send<T>` serializes
through the wire format and enqueues the resulting bytes; `Receive<T>` dequeues
and deserializes. It exercises the wire **serialization** fully with zero
network I/O; framing, keepalive replacement (session heartbeats), and per-type
demux are `TcpTransport` behavior.

## `TcpTransport` (peer / LAN / dedicated)

`Transport/TcpTransport.cs` is a socket peer usable as a client
(`Connect(host, port)`; the async connect is bounded by `Transport.TimeoutMs`
so an unreachable host fails the join attempt instead of freezing the caller
through the OS connect retry schedule) or a server peer (`Listen(port)`; the
multi-peer acceptor is `TcpServerHost`). It length-prefixes each serialized
message with a type tag and dispatches frames to a **per-type receive queue**
on a background receive thread, so each polling system can `Receive<T>` a
distinct type. Framing is pool-backed on the hot path: wire frames and the
coalesced send segment come from `ArrayPool<byte>` and return after write, and
type tags resolve to bytes once at registration (`SerializerCache`) instead of
re-encoding per send.

Sends are **non-blocking**: each `Send<T>` serializes and frames the message on
the calling thread, enqueues the bytes, and returns. A dedicated background
send thread drains the queues in priority order and writes the socket. Control
and state messages (join, world stream, level save requests, chat, voxel edits,
notifications, skill updates) ride a **never-evicting reliable queue**; a dropped
frame there
is permanent data loss, so a peer that stops reading surfaces as a
grow-and-error condition instead (`ReliableQueueConcernThreshold`, re-logged
every ~100 frames). Per-tick snapshot traffic rides the bounded `SendQueue`
whose drop-oldest policy applies only to it — a full queue drops the oldest
snapshot frame, so input staleness is bounded instead of the queue growing
without limit. The classification is explicit (`SendPriority`): every new
per-tick message must be deliberately marked droppable. This keeps a dead
peer from blocking the game loop, and the socket `SendTimeout`/
`ReceiveTimeout` bound any stalled read or write. A peer whose reliable
backlog stays over `ReliableQueueDisconnectThreshold` for
`ReliableQueueDisconnectMs` is dropped outright, bounding per-peer memory on
a peer that never reads (see [join](networking-join.md) for the client-side
join-stream timeout).

A keepalive heartbeat keeps a live-but-idle peer from being dropped. There is
no transport-level keepalive frame: the **session heartbeat** is the liveness
signal. Both sides emit an app-level heartbeat per connection at
`NetworkingConfig.Protocol.HeartbeatIntervalMs` (clamped below
`Transport.TimeoutMs`) from connection establishment - server → client `ServerHeartbeatMessage`
(carrying the averaged `TPS`, the current sim `TickNumber`, and
`PlayerCount`), client → server `ClientHeartbeatMessage` (carrying the client's
sim tick and last applied snapshot tick). The receive loop no longer skips any
frame kind; a genuinely idle link times out. The server emits its half from two
pumps so every accepted connection stays readable: the per-world
`ServerHeartbeatService` for connections bound to a world hub, and the
host-level `ServerHostHeartbeat` (`Server.Core/ServerHostHeartbeat.cs`) for
connections still in the pending set - the menu and character-creation window,
before any world exists. The host-level pump also drains the pending
connection's client heartbeats, so they cannot pile up unread. The server
tracks each client's reported tick and logs a warn when it falls behind by
more than `Server.TickLagWarnThreshold` sim ticks (once per crossing).

Disconnect detection is symmetric. Either the receive or the send thread can
observe the peer is gone (EOF, a read/write exception, or a timeout). Because a
live link keeps breathing via keepalive, a read/write timeout now only fires for
a genuinely gone peer; the first thread to observe it cancels the other, marks
the transport broken, and raises `OnDisconnected` exactly once. The server host
drops the peer from its hub and prunes its own registry; its owner (`LanHost`
server-side, `TransportManager` client-side) disposes the dead transport —
closing the socket exactly once — and `IsConnected` flips to false. The
transport never disposes itself.

## Transport counters

`Transport/IConnectionCounters.cs` exposes four monotonic counters:
`PacketsSent`, `PacketsReceived`, `BytesSent`, and `BytesReceived`. Both
`LocalConnection` endpoints and `TcpTransport` implement the interface.

- `TcpTransport` counts on the real I/O paths. The send loop adds one packet
  and the summed byte length per socket write (`TcpTransport.cs:492`); the
  receive loop counts every frame that arrives at the I/O boundary
  (`TcpTransport.cs:536`), including a frame it drops for an unknown or
  malformed type tag. The 4-byte length prefix is included on both sides, so a
  peer's `BytesSent` matches the other peer's `BytesReceived`.
- `LocalConnection` keeps counters per endpoint, so each side reports its own
  direction: the client endpoint counts client-to-server sends and
  server-to-client receives, and the server endpoint counts the reverse
  (`LocalConnection.cs:33-34`).

The F3 `NetworkStatsOverlay`
(`WaywardBeyond.Client/UI/NetworkStatsOverlay.cs`) reads the active
transport's counters. It shows the **average** packets and bytes per second and
the plain totals. It uses the average, not the median, because network traffic
is bursty and the median of per-frame samples reads zero when packets arrive in
fewer frames than the render loop runs.

## `ServerConnectionHub` (multi-client)

`Transport/ServerConnectionHub.cs` aggregates one `IServerConnection` per
connected client under an opaque `Uuid` (`clientId`) assigned on `Add`. It is
shared code because the shared transports feed it and every world side consumes it.

- `Receive<T>()` polls every client connection, draining at most
  `NetworkingConfig.Server.MaxReceiveWindow` frames per client per poll so one
  chatty client cannot starve the rest of a server tick, and tags each
  inbound message with the `clientId` it arrived on.
- `Send<T>(clientId, ...)` addresses a single client.
- `Remove(clientId)` queues a disconnect that `DrainDisconnects()` surfaces to
  the server teardown step.

## Sessions

`Server.Core/SessionManager.cs` binds the full
`clientId ↔ Session ↔ player entity` chain, stamping `NetworkComponent.Session`.

- `ServerJoinSystem` allocates a `Session` per connection and calls `Register`.
- `NetworkReplicationSystem` uses `SessionManager.TryGetEntity(clientId, ...)`
  to read that client's per-entity acked input.
- Disconnect teardown (the first stage of each world's `ServerJoinSystem`)
  disposes the mirror's physics body, captures its uuid, frees the entity, and
  clears the mapping.
  The despawn broadcasts in `RemovedEntities`. The despawn uuid is captured
  **before** the free because `DataStore.Free` clears the uuid.

## LAN server discovery

Hosts advertise an open server over **UDP broadcast**
so a LAN client can auto-populate the multiplayer page. This is the one
permitted UDP exception to the TCP-only rule; the beacon is a pure control
plane and never carries game state.

- **Server broadcast.** `LanHost` (`Server.Core/LanHost.cs`) starts a
  `"LAN BEACON"` thread that sends an nsd `LanBeacon` to `255.255.255.255`
  every `NetworkingConfig.Discovery.BroadcastSeconds`. `PlayerCount` is read
  live from `ServerWorldHost.PlayerCount` (the sum over world hubs; each
  accepted peer awaits its join in the pending set until it binds to a world).
  A `SocketException` (broadcast blocked) kills the loop permanently.
- **Client scan.** `LanDiscoveryService` (`Client.Core/Networking/`) opens a
  `UdpClient` on the same port and streams each discovered server as an
  `IAsyncEnumerable<DiscoveredServer>` while listening for
  `Discovery.ScanDurationSeconds`. It drops non-matching protocol versions, dedupes by
  endpoint, and reads off the UI thread. It ignores a beacon from its own
  in-process server by source address + advertised TCP port.
- `MultiplayerPage` pre-fills host/port so the normal
  `TransportManager.ConnectRemote` join path is reused.

## Configuration

`NetworkingConfig` (`WaywardBeyond.Config/NetworkingConfig.cs`), loaded from
`network.toml`. The remote endpoint keys are top-level. The rest group into
nested `[Server]`, `[Discovery]`, `[Transport]`, and `[Protocol]` tables.

Top-level:

| Key | Default |
|---|---|
| `RemoteHost` | `127.0.0.1` |
| `RemotePort` | `7777` |

`[Server]` (`ServerConfig`):

| Key | Default |
|---|---|
| `Port` | `0` |
| `Name` | `LAN Server` |
| `IdleUnloadMs` | `60000` |
| `TickLagWarnThreshold` | `10` |
| `MaxReceiveWindow` | `10` |

`[Discovery]` (`DiscoveryConfig`):

| Key | Default |
|---|---|
| `Enabled` | `true` |
| `Port` | `47777` |
| `BroadcastSeconds` | `5` |
| `ScanDurationSeconds` | `20` |

`[Transport]` (`TransportConfig`):

| Key | Default |
|---|---|
| `TraceLogging` | `false` |
| `TimeoutMs` | `5000` |
| `MaxFrameBytes` | `16777216` |
| `SendIntervalMs` | `16` |
| `SendQueueSize` | `256` |
| `ReliableQueueConcernThreshold` | `64` |
| `ReliableQueueDisconnectThreshold` | `128` |
| `ReliableQueueDisconnectMs` | `10000` |

`[Protocol]` (`ProtocolConfig`):

| Key | Default |
|---|---|
| `HeartbeatIntervalMs` | `1000` |
| `SnapshotHz` | `30` |
| `JoinStreamTimeoutMs` | `60000` |

Disconnect detection is reason-carrying: `TcpTransport.OnDisconnected` reports
a `DisconnectReason` (peer-closed EOF, read/write error, read/write timeout, or
backlog limit), logged by the transport and its consumers (`LanHost`,
`TransportManager`). Per-message tracing (every sent and received frame's type
and byte count, at trace level) is gated by `TraceLogging`, so production logs
stay clean. Frames dropped for unknown type tags and decode failures log at
warn with the discriminating type name and byte count.

Frames are length-prefixed with a 4-byte body length. The receive loop rejects
a prefix over `MaxFrameBytes` as a protocol violation and drops the connection
before allocating the frame buffer; `Send<T>` refuses frames over the cap. The
cap also bounds the deserializer's worst-case wire reach (see
[nsdc frame bounds](#nsdc-frame-bounds)).

### Per-client isolation

`NetworkReplicationSystem.ApplyStage` processes every inbound snapshot inside a
per-client try/catch: a malformed payload from one client is logged and
skipped, and the remaining clients plus the world step continue. The blanket
guard in `ServerWorldHost.Update` stays as the last line of defense.

### nsdc frame bounds

The nsdc-generated deserializers (`CodeGen/Output/*.cs`) bound internal reads
against attacker-derived lengths; the structural fix requires an upstream nsdc
change, and the submission (exact locations + suggested guard shape) is tracked
as [issue 0037](/docs/issues/0037-nsdc-unpack-safety.md). Until the fixed
codegen lands, `MaxFrameBytes` bounds the reachable buffer size and
per-client isolation keeps a decode fault from halting the server.

## Peer scaling (2N+1 threads)

Each `TcpTransport` peer runs **two** background threads (receive, send) plus
the host's accept thread - 2N+1 network threads for N connected remote
clients, plus the server world thread. The transport keepalive thread was
removed with the session heartbeat (see above), so the count is lower than
earlier estimates. Per-peer memory is bounded: the per-tick send queue holds
at most `SendQueueSize` frames (drop-oldest), the reliable queue grows only
when a peer stops reading (bounded by the backlog-disconnect policy), and the
receive queues are drained per tick at `MaxReceiveWindow` per client.

This layout is the simplest correct design for blocking I/O and is fine for
the LAN co-op target (N ≤ 8: 17 network threads). For a dedicated server
heading toward dozens of clients, the options are a single async receive loop
(`SocketAsyncEventArgs`/`ValueTask`) multiplexing all peers against per-peer
queue state, or a documented hard cap on the thread model. **Decision: keep
the per-peer thread model for the current target and cap the dedicated server
at 32 clients (65 network threads); revisit async multiplexing only when a
live 32-player dedicated server exists and measurements show thread count or
per-peer memory is the bottleneck** (the #0033 counters + `NetworkStatsOverlay`
already provide the per-peer bytes/rates needed for that measurement).

## Dedicated server

A headless dedicated server is a Shoal embedding without the client module:
`WaywardBeyond.Server.Launcher` (console `Exe`) loads the server and shared
modules only (`shared.bricks`, `shared.skills`, `shared.bodies`,
`server.core`) - no window, input, or client-level services. It registers the
same shared host wire-up as the embedded client host
(`Server.Core/HostComposition.cs`): the serializer set, `NetworkRegistry`
init, hub (no loopback seed), save storage, `NetworkingConfig`, and
`PhysicsSettings`; `ServerModule`'s host registrations add
`ServerWorldHost` + `LanHost` + the beacon. Interaction content is the
embedding's choice: the client module registers its item-backed content, the
launcher registers `ServerInteractionContent` (breaks and loot; place
resolution needs shared item content). Lifecycle: `Ctrl+C`/SIGTERM → clean
shutdown (level flush, session teardown, store disposal, per
[persistence](persistence.md)). CLI: `--name` and `--port` override
`NetworkingConfig.Server.Name`/`Server.Port`, and `--data` overrides
`StorageSettings.SaveRoot`.

## Source of truth

- `WaywardBeyond.Networking/Transport/{LocalConnection,TcpTransport,TcpServerHost,ServerConnectionHub}.cs`
- `WaywardBeyond.Server/SessionManager.cs`
- `WaywardBeyond.Server/LanHost.cs`
- `WaywardBeyond.Server/ServerHostHeartbeat.cs`
- `WaywardBeyond.Client/Networking/LanDiscoveryService.cs`
- `WaywardBeyond.Client/Networking/TransportManager.cs`
- `WaywardBeyond.Client/Systems/ClientDisconnectSystem.cs`
- `WaywardBeyond.Config/NetworkingConfig.cs`

## Tests that pin this

- `Swordfish.Tests/LocalConnectionTests.cs` covers the local serialize/dequeue
  round trips without a socket.