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
and deserializes. This exercises the full protocol with zero network I/O.

## `TcpTransport` (peer / LAN / dedicated)

`Transport/TcpTransport.cs` is a socket peer usable as a client
(`Connect(host, port)`; the async connect is bounded by `ConnectionTimeoutMs`
so an unreachable host fails the join attempt instead of freezing the caller
through the OS connect retry schedule) or a server peer (`Listen(port)`; the
multi-peer acceptor is `TcpServerHost`). It length-prefixes each serialized
message with a type tag and dispatches frames to a **per-type receive queue**
on a background receive thread, so each polling system can `Receive<T>` a
distinct type.

Sends are **non-blocking**: each `Send<T>` serializes and frames the message on
the calling thread, enqueues the bytes, and returns. A dedicated background
send thread drains the queues in priority order and writes the socket. Control
and state messages (join, world stream, chat, voxel edits, notifications,
skill updates) ride a **never-evicting reliable queue**; a dropped frame there
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

A keepalive heartbeat keeps a live-but-idle peer from being dropped. Each peer
runs a dedicated background thread that enqueues an empty-type-tag frame every
`KeepaliveIntervalMs`, so both receive directions always deliver a readable byte
within the `ConnectionTimeoutMs` window. The receive loop silently skips frames
with an empty type tag. Without keepalive, a quiet client whose server only
publishes on change would idle a full read timeout and be misread as dead over a
high-latency link (for example tailscale).

Disconnect detection is symmetric. Either the receive or the send thread can
observe the peer is gone (EOF, a read/write exception, or a timeout). Because a
live link keeps breathing via keepalive, a read/write timeout now only fires for
a genuinely gone peer; the first thread to observe it cancels the other, marks
the transport broken, and raises `OnDisconnected` exactly once. The server host
drops the peer from its hub and prunes its own registry; its owner (`LanHost`
server-side, `TransportManager` client-side) disposes the dead transport —
closing the socket exactly once — and `IsConnected` flips to false. The
transport never disposes itself.

## `ServerConnectionHub` (multi-client)

`Transport/ServerConnectionHub.cs` aggregates one `IServerConnection` per
connected client under an opaque `Uuid` (`clientId`) assigned on `Add`. It is
shared code because the shared transports feed it and every world side consumes it.

- `Receive<T>()` polls every client connection, draining at most
  `NetworkingSettings.MaxReceiveWindow` frames per client per poll so one
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
- Disconnect teardown (`ServerContext.HandleDisconnects`) disposes the mirror's
  physics body, captures its uuid, frees the entity, and clears the mapping.
  The despawn broadcasts in `RemovedEntities`. The despawn uuid is captured
  **before** the free because `DataStore.Free` clears the uuid.

## LAN server discovery

Hosts in `NetworkMode.Host` advertise an open server over **UDP broadcast**
so a LAN client can auto-populate the multiplayer page. This is the one
permitted UDP exception to the TCP-only rule; the beacon is a pure control
plane and never carries game state.

- **Server broadcast.** `LanHost` (`Server.Core/LanHost.cs`) starts a
  `"LAN BEACON"` thread that sends an nsd `LanBeacon` to `255.255.255.255`
  every `NetworkingSettings.DiscoveryBroadcastSeconds`. `PlayerCount` is read
  live from `ServerConnectionHub.Clients`. A `SocketException` (broadcast
  blocked) kills the loop permanently.
- **Client scan.** `LanDiscoveryService` (`Client.Core/Networking/`) opens a
  `UdpClient` on the same port and streams each discovered server as an
  `IAsyncEnumerable<DiscoveredServer>` while listening for
  `DiscoveryScanSeconds`. It drops non-matching protocol versions, dedupes by
  endpoint, and reads off the UI thread. It ignores a beacon from its own
  in-process server by source address + advertised TCP port.
- `MultiplayerPage` pre-fills host/port so the normal
  `TransportManager.ConnectRemote` join path is reused.

## Configuration

`NetworkingSettings` (`WaywardBeyond.Shared.Config/NetworkingSettings.cs`),
loaded from `network.toml`:

| Key | Default |
|---|---|
| `ServerPort` | `0` |
| `DefaultHost` | `127.0.0.1` |
| `DefaultConnectPort` | `7777` |
| `ServerName` | `LAN Server` |
| `DiscoveryPort` | `47777` |
| `LanDiscovery` | `true` |
| `DiscoveryBroadcastSeconds` | `5` |
| `DiscoveryScanSeconds` | `20` |
| `ConnectionTimeoutMs` | `5000` |
| `KeepaliveIntervalMs` | `2000` |
| `SendQueueSize` | `256` |
| `MaxFrameBytes` | `16777216` |
| `ReliableQueueConcernThreshold` | `64` |
| `ReliableQueueDisconnectThreshold` | `128` |
| `ReliableQueueDisconnectMs` | `10000` |
| `JoinStreamTimeoutMs` | `60000` |
| `MaxReceiveWindow` | `10` |
| `TraceLogging` | `false` |

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
guard in `ServerContext.Update` stays as the last line of defense.

### nsdc frame bounds

The nsdc-generated deserializers (`CodeGen/Output/*.cs`) bound internal reads
against attacker-derived lengths; the structural fix requires an upstream nsdc
change, and the submission (exact locations + suggested guard shape) is tracked
as [issue 0037](/docs/issues/0037-nsdc-unpack-safety.md). Until the fixed
codegen lands, `MaxFrameBytes` bounds the reachable buffer size and
per-client isolation keeps a decode fault from halting the server.

## Source of truth

- `WaywardBeyond.Shared.Networking/Transport/{LocalConnection,TcpTransport,TcpServerHost,ServerConnectionHub}.cs`
- `WaywardBeyond.Server.Core/SessionManager.cs`
- `WaywardBeyond.Server.Core/LanHost.cs`
- `WaywardBeyond.Client.Core/Networking/LanDiscoveryService.cs`
- `WaywardBeyond.Client.Core/Networking/TransportManager.cs`
- `WaywardBeyond.Client.Core/Systems/ClientDisconnectSystem.cs`
- `WaywardBeyond.Shared.Config/NetworkingSettings.cs`

## Tests that pin this

- `Swordfish.Tests/SessionRoutingTests.cs` exercises N-client routing over the
  hub + `LocalConnection` fixture without a socket.