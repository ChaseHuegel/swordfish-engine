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

`Transport/TcpTransport.cs` is a single-threaded TCP peer (`Connect(host, port)`
for client mode, `Listen(port)` for server mode). It length-prefixes each
serialized message and reads frames on a background thread into a **single
shared receive queue**. It has no per-type demux, so it cannot yet host the
polling systems that each `Receive<T>` a distinct type. It is unexercised in
production code.

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

## Source of truth

- `WaywardBeyond.Shared.Networking/Transport/{LocalConnection,TcpTransport,TcpServerHost,ServerConnectionHub}.cs`
- `WaywardBeyond.Server.Core/SessionManager.cs`
- `WaywardBeyond.Server.Core/LanHost.cs`
- `WaywardBeyond.Client.Core/Networking/LanDiscoveryService.cs`
- `WaywardBeyond.Shared.Config/NetworkingSettings.cs`

## Tests that pin this

- `Swordfish.Tests/SessionRoutingTests.cs` exercises N-client routing over the
  hub + `LocalConnection` fixture without a socket.