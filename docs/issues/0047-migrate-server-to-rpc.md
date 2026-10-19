# Improvement: Migrate the server to nsd-rpc services

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

The server polls per-type receive queues from `ServerConnectionHub`, matches
menu replies by FIFO order, and sends every message through `Send<T>`. The
join stream bursts a whole world in one tick. A handler runs on the server
thread only by accident of where the poll happens. None of this is enforced.

The RPC layer moves dispatch to its own worker. The server must make store
ownership explicit. Handlers stage work and the owning world tick performs it.
The server also needs a peer record that holds the game id, the claim, the
world binding, the generated client proxy, and the connection stats.

## Acceptance criteria

- [ ] `ServerPeer` holds the game `Uuid`, the `RpcPeer`, a cached
      `GameClientApiClient`, the remote endpoint, the latest
      `RpcConnectionStats`, the last `RpcDisconnectReason`, `IsLocal`, the
      claim, the bound world, and the last inbound timestamp.
- [ ] `ServerPeers` keys peers by game `Uuid` and by `(IRpcHost, clientId)`.
      Library client ids from two `RpcServer` instances cannot collide.
- [ ] `ServerRpcHost` owns one frozen registry with `ServerHandlers`, one
      loopback `RpcServer`, and one LAN `RpcServer` over `TcpServerTransport`.
      It starts both, wires `ClientConnected` and `ClientDisconnected`, polls
      call deadlines, and closes both on dispose.
- [ ] A wrapping `IRpcServerTransport` records each accepted connection's
      disconnect reason by `ClientId` and returns the original connection.
      The RPC peer keeps the owned-frame send path.
- [ ] `ClientDisconnected` handling is idempotent. It logs the recorded reason
      and the remote endpoint.
- [ ] `ServerHandlers` only reads the context and enqueues. No handler touches
      a `DataStore`.
- [ ] `ServerHostCommands` runs on the server thread. It handles world routing,
      join, leave, and the menu-level create, list, and delete calls.
      `ILevelCatalog` access stays single-threaded. The in-world save request
      stays in the world tick.
- [ ] A menu call returns a correlated `RpcResult<T>`. `LevelsClient` FIFO
      matching and `ServerLevelManager._replyChains` are removed.
- [ ] `JoinAsync` returns `JoinAccept` after the world creates the session and
      the mirror.
- [ ] `StreamLevelAsync` streams `LevelStreamChunk` fragments from the server
      thread. It checks the handler token and `RpcPeer.Stats`, uses
      `RpcContext.TryStream` for each fragment, and aborts when a send is
      rejected. It sends a bounded byte budget per tick
      (`Protocol.LevelStreamBytesPerTick`). It ends with
      `RpcResult<LevelStreamComplete>`.
- [ ] `BeginStream` and `EndStream` gate per-tick snapshots until the stream
      completes. The full-sync snapshot is still the client's first snapshot.
- [ ] Replication publishes a typed `WorldSnapshot` per client. If the payload
      exceeds the fragment budget, it sends `SnapshotBatch` parts. No snapshot
      is dropped. A send rejected at the hard cap closes the peer with
      `RpcPeer.Close()` and logs the failure.
- [ ] The raw snapshot frame patch and `SendRaw` path are removed.
- [ ] Server and client heartbeats use notifications. Client heartbeats update
      the peer's last inbound timestamp. The transport idle timeout detects a
      half-open peer. The heartbeat cadence clamps below
      `Transport.IdleTimeoutMs`.
- [ ] Chat, interaction edits, save results, notifications, and skill updates
      send through each peer's cached client proxy. No send result is ignored.
- [ ] `LanHost` starts the TCP RPC server. The UDP discovery beacon is
      unchanged.
- [ ] `ServerConnectionHub`, `ConnectionClaims`, `PendingLevelDeletes`, and the
      old join queue paths are removed from the server.
- [ ] A loopback test covers menu calls, join, stream, and leave. A TCP test
      covers the same path over `127.0.0.1:0`.
- [ ] A stalled-peer test proves a sustained backlog closes the peer instead of
      dropping a snapshot.
- [ ] The docs pass updates `networking-worlds`, `networking-join`,
      `networking-replication`, `networking-transports`, `chat`, and
      `permissions`.

## Dependencies

- 0046 adds the protocol layer, services, fragments, and loopback transport.
- nsd-rpc 0.3.0 supplies the backlog policy, idle timeout, and metrics.
