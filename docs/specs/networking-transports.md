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
(`Connect(host, port)`) or a server peer (`Listen(port)`; the multi-peer
acceptor is `TcpServerHost`). It length-prefixes each serialized message with a
type tag and dispatches frames to a **per-type receive queue** on a background
receive thread, so each polling system can `Receive<T>` a distinct type.

Sends are **non-blocking**: each `Send<T>` serializes and frames the message on
the calling thread, enqueues the bytes to a bounded `BlockingCollection`, and
returns. A dedicated background send thread drains the queue in FIFO order and
writes the socket. This keeps a dead peer from blocking the game loop: a full
send queue drops the oldest frame instead of growing, and the socket
`SendTimeout`/`ReceiveTimeout` bound any stalled read or write.

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
drops the peer from its hub; the client returns to the menu (see
`ClientDisconnectSystem`).

## `ServerConnectionHub` (multi-client)

`Transport/ServerConnectionHub.cs` aggregates one `IServerConnection` per
connected client under an opaque `Uuid` (`clientId`) assigned on `Add`. It is
shared code because the shared transports feed it and every world side consumes it.

- `Receive<T>()` polls every client connection and tags each inbound message
  with the `clientId` it arrived on.
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