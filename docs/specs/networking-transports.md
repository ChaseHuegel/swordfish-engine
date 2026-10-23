# Networking — Transports, Sessions, LAN

One subject: how RPC frames move, how sessions map to peers, and LAN discovery.
The wire catalog lives in [networking-rpc](networking-rpc.md).

## Transport layers

| Layer | Owner | Purpose |
|---|---|---|
| envelope, correlation, framing | `NsdRpc` | one reliable frame stream per connection |
| TCP sockets | `NsdRpc.Tcp` | loopback and LAN transports |
| service catalogue, handlers, peer directory | the game | game methods and world ownership |
| loopback | the game | in-process server for singleplayer and host mode |

`WaywardBeyond.Networking` references `NsdRpc`, `NsdRpc.Tcp`, and
`NsdRpc.Generators` at 0.3.0. The game no longer owns a wire envelope, type
tags, per-type receive queues, or FIFO request matching.

## TCP

`NsdRpc.Tcp` provides `TcpClientConnection` and `TcpServerTransport`.

- One reliable send queue per connection. A frame is never dropped after the
  queue accepts it.
- `SendQueueBytes` is the hard cap. A send at the cap is rejected, and the
  caller treats the rejection as a peer failure.
- `SendBacklogDisconnectBytes` and `SendBacklogDisconnectMs` close a stalled
  peer with `BacklogLimit`.
- `IdleTimeoutMs` closes a half-open peer with `IdleTimeout`.
- `ConnectTimeoutMs` bounds a connect attempt with `TimeoutException`.
- `SendBatchIntervalMs` coalesces queued frames into one socket write, up to
  `SendBatchMaxBytes`.
- `RpcPeer.Stats` reports frames, bytes, queued and peak bytes, writes, and
  rejected sends.
- nsd-rpc drop and error events go to `RpcOptions.LogSink`.

## Loopback

`WaywardBeyond.Networking/Transport/LoopbackRpc*.cs` implements the nsd-rpc
transport interfaces for the in-process host.

- `LoopbackRpcServerTransport` queues every connection and accepts them in order.
  It stays open when a client closes, so a new client connection gets a new id.
- `LoopbackRpcClientConnection` connects and pairs with a server endpoint.
- `LoopbackRpcServerConnection` is the server half. Its remote endpoint is null.
- A receiving endpoint buffers frames until the peer subscribes to
  `FrameReceived`, so a hello sent before the server peer is wired is not lost.
  One thread at a time drains the buffer in send order; senders never wait for
  a callback.
- The pending buffer is capped. A peer that exceeds it is closed with
  `ProtocolViolation`.
- `AcceptAsync` returns null once the transport is closed.
- `Disconnected` fires once. The closing side passes its reason. The peer sees
  `PeerClosed`.
- Each side reports its own `IRpcConnectionStats` counters.

## Client host

`WaywardBeyond.Client/Networking/ClientRpcHost.cs` owns the active session.

- Startup: connect the loopback, build the `RpcClient`, cache the
  `GameServerApiClient`, send `ClientHello`.
- `ConnectRemote`: connect TCP first, then close the loopback and switch. A
  failed connect leaves the loopback active.
- Remote drop: record the `RpcDisconnectReason`, raise `RemoteDisconnected`,
  then create a fresh loopback client connection. The menu lists and loads
  local saves without an application restart.
- `Disconnect`: closes a remote peer and restores the loopback. An active
  loopback is left alone.
- `Poll` enforces call deadlines once per client tick.
- `Stats` is the active connection's `RpcConnectionStats` or zeros.
- `ClientInbound` clears on every connection switch.

## Server host

`WaywardBeyond.Server/ServerRpcHost.cs` owns `ServerPeers`, one frozen registry
with `ServerHandlers`, the loopback `RpcServer`, and the LAN `RpcServer`.

- `Run` starts the loopback server and the deadline poll thread.
- `StartLan(port)` starts the TCP server. `LanHost` calls it and then starts
  the UDP discovery beacon.
- A `RecordingRpcServerTransport` records each accepted connection's disconnect
  reason by `ClientId` and returns the original connection. The nsd-rpc peer
  keeps the owned-frame send path.
- `ClientConnected` adds the peer. A handler can create the peer first if a
  buffered frame dispatches before the connect event.
- `ClientDisconnected` is idempotent. It records the reason, removes the peer,
  and queues teardown for the server thread.

## Sessions

`ServerPeer` holds the stable game `Uuid`, the `RpcPeer`, the cached client
proxy, the remote endpoint, the stats, the last disconnect reason, `IsLocal`,
the claim, the bound world, and the last inbound stamp.

`WaywardBeyond.Server/SessionManager.cs` binds
`game Uuid ↔ Session ↔ player entity` and stamps `NetworkComponent.Session`.
`Session` and `NetworkComponent` live in `WaywardBeyond.Gameplay/Networking/`.

- `ServerJoinSystem` allocates a `Session` per peer and calls `Register`.
- `NetworkReplicationSystem` reads each peer's acked input through
  `SessionManager.TryGetEntity`.
- Disconnect teardown runs on the server thread. It disposes the mirror's
  physics body, captures the uuid before `DataStore.Free`, frees the entity,
  and clears the mapping. The despawn broadcasts in `RemovedEntities`.

## Heartbeats

Session heartbeats are RPC notifications, not transport keepalives.

- The server sends `ServerHeartbeat` to bound peers from
  `ServerHeartbeatService`, and to unbound menu peers from
  `PendingConnectionHeartbeat`.
- The client sends `ClientHeartbeat` from `ClientHeartbeatSystem`.
- Every inbound handler stamps `ServerPeer.LastInboundTicks`.
- The heartbeat cadence is clamped below `IdleTimeoutMs`.
- The server logs a warning when a client's reported sim tick falls behind by
  more than `Server.TickLagWarnThreshold`.

## LAN server discovery

Hosts advertise an open server over UDP broadcast so a LAN client can populate
the multiplayer page. The beacon is a control plane and never carries game
state.

- `LanHost` starts the LAN RPC listener and a `"LAN BEACON"` thread. The thread
  sends an nsd `LanBeacon` to `255.255.255.255` every
  `Discovery.BroadcastSeconds`. `PlayerCount` is the sum of world session
  counts.
- `LanDiscoveryService` listens on the discovery port for
  `Discovery.ScanDurationSeconds`, drops non-matching protocol versions, dedupes
  by endpoint, and ignores its own server by source address and TCP port.
- `LanDiscovery.PROTOCOL_VERSION` is 3. An old client cannot join a new server.
- `MultiplayerPage` pre-fills host and port and uses the normal
  `ClientRpcHost.ConnectRemote` path.

## Configuration

`NetworkingConfig` (`WaywardBeyond.Config/NetworkingConfig.cs`) loads from
`network.toml`. The remote endpoint keys are top-level. The rest group into
`[Server]`, `[Discovery]`, `[Transport]`, and `[Protocol]`.

`WaywardBeyond.Config/NetworkingConfigExtensions.cs` maps the config onto nsd-rpc
through `ToRpcOptions` and `ToTcpOptions`. `ConfigComposition` validates the
transport policy once at startup and fails fast.

Top-level:

| Key | Default |
|---|---|
| `RemoteHost` | `127.0.0.1` |
| `RemotePort` | `7777` |

`[Server]`:

| Key | Default |
|---|---|
| `Port` | `0` |
| `Name` | `LAN Server` |
| `IdleUnloadMs` | `60000` |
| `TickLagWarnThreshold` | `10` |

`[Discovery]`:

| Key | Default |
|---|---|
| `Enabled` | `true` |
| `Port` | `47777` |
| `BroadcastSeconds` | `5` |
| `ScanDurationSeconds` | `20` |

`[Transport]`:

| Key | Default | Meaning |
|---|---|---|
| `ConnectTimeoutMs` | `5000` | one connect attempt |
| `IdleTimeoutMs` | `5000` | close a silent peer |
| `SendBatchIntervalMs` | `16` | coalesce frames per write; zero sends at once |
| `SendBatchMaxBytes` | `65536` | largest batched write |
| `SendQueueBytes` | `4194304` | hard send queue cap |
| `SendBacklogDisconnectBytes` | `1048576` | backlog window start |
| `SendBacklogDisconnectMs` | `10000` | backlog window length |

`[Protocol]`:

| Key | Default | Meaning |
|---|---|---|
| `HeartbeatIntervalMs` | `1000` | session heartbeat cadence |
| `SnapshotHz` | `30` | snapshot and upload cadence |
| `JoinStreamTimeoutMs` | `60000` | client join and stream deadline |
| `CallTimeoutMs` | `30000` | default RPC call deadline |
| `MaxPendingCalls` | `1024` | pending calls and inbound requests per peer |
| `MaxBufferedChunks` | `1024` | buffered chunks per stream session |
| `LevelStreamBytesPerTick` | `262144` | level stream budget per server tick |

`SendBacklogDisconnectBytes` must not exceed `SendQueueBytes`. The backlog keys
are both zero or both positive. An invalid combination fails at startup through
`NetworkingConfig.Validate()`.

`ToRpcOptions` pins `MaxFrameBytes` to 65,570. The fragment budget stays below it.

## Threading

The nsd-rpc peer runs one dispatch pump per connection. The game runs one server
thread for all worlds. `ServerHandlers` and `ClientHandlers` only enqueue.
`ServerHostCommands` and the world systems run on the server thread. The old
2N+1 thread model is gone.

## Dedicated server

`WaywardBeyond.Server.Launcher` loads the server and shared modules only. The
server module registers `ServerWorldHost`, `ServerRpcHost`, and `LanHost`.
There is no loopback client and no client module. The dedicated
`config/network.toml` uses the schema above. `Ctrl+C`/SIGTERM runs the clean
shutdown: level flush, session teardown, store disposal. A LAN listen failure
exits non-zero.

## Source of truth

- `WaywardBeyond.Networking/Rpc/`
- `WaywardBeyond.Networking/Transport/LoopbackRpc*.cs`
- `WaywardBeyond.Server/{ServerPeers,ServerPeer,ServerRpcHost}.cs`
- `WaywardBeyond.Server/PendingConnectionHeartbeat.cs`
- `WaywardBeyond.Server/LanHost.cs`
- `WaywardBeyond.Server/SessionManager.cs`
- `WaywardBeyond.Client/Networking/{ClientRpcHost,ClientInbound,ClientHandlers,LanDiscoveryService}.cs`
- `WaywardBeyond.Config/{NetworkingConfig,NetworkingConfigExtensions,TransportConfig,ProtocolConfig,ServerConfig}.cs`
- `WaywardBeyond.Server.Launcher/config/network.toml`
