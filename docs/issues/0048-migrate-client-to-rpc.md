# Improvement: Migrate the client to nsd-rpc services

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

Client systems poll `IClientConnection.Receive<T>` per message type. The
transport manager swaps the active connection by changing a field. Menu
requests are matched to responses by response type and order. Every system
knows the transport type.

The RPC layer delivers inbound messages on its own worker. The client must
stage them and apply them on the ECS thread. The client also needs one stable
host that owns the active `RpcClient`, the generated server proxy, the inbound
queues, and the connection stats.

## Acceptance criteria

- [ ] `ClientRpcHost` replaces `TransportManager` and `IClientConnection`. It
      owns the frozen client registry, the active `RpcClient`, the
      `GameServerApiClient`, the connection, the inbound queues, and the stats.
- [ ] `ClientRpcHost` subscribes to the connection `Disconnected` event and
      records the `RpcDisconnectReason`. It exposes the reason for logging and
      teardown. It raises `RemoteDisconnected` for a reason other than
      `LocalClose`. `Stats` comes from `IRpcConnectionStats` or
      `RpcPeer.Stats`.
- [ ] `ClientInbound` holds per-type queues for snapshots, snapshot batches,
      heartbeats, voxel edits, notifications, skill updates, chat, and level
      stream chunks. It clears on every connection switch.
- [ ] `ClientHandlers` only enqueues. No handler touches a `DataStore`.
- [ ] The client connects to the loopback at startup, sends `ClientHello`, and
      polls call deadlines each tick.
- [ ] `ConnectRemote` creates a `TcpClientConnection`, honors the connect
      timeout, builds a new `RpcClient`, and sends `ClientHello`.
- [ ] `ClientJoinSystem` runs the two-call flow. `JoinAsync` seats the player.
      `StreamLevelAsync` uses an `IChunkSink<LevelStreamChunk>` that enqueues
      fragments. The ECS tick reassembles and builds view entities. The system
      enters `Playing` only after `LevelStreamComplete`.
- [ ] `JoinStreamTimeoutMs` cancels the join. A timeout or a failed result runs
      the normal disconnect teardown.
- [ ] `ClientReconcileSystem` drains and reassembles inbound snapshots. It keeps
      the entering-play burst coalescing. It applies a batch only when the
      batch is complete and drops a stale incomplete batch.
- [ ] `ClientVoxelReconcileSystem`, `ClientNotificationSystem`,
      `ClientChatSystem`, and `ClientHeartbeatSystem` drain `ClientInbound`.
      They send through the generated proxy.
- [ ] `ClientReplicationSystem` sends the upstream snapshot as a reliable
      notification. It clears staged edges and ops only after the send path
      accepts the snapshot. A rejected send is treated as a peer failure.
- [ ] `LevelsClient` is a thin async wrapper over `GameServerApiClient`. FIFO
      queues and `FaultPending` are removed. `RpcClient` faults pending calls on
      disconnect.
- [ ] `ClientLevelServiceSystem`, `ClientJoinSystem`, `ClientDisconnectSystem`,
      `GameSaveManager`, and the menu pages use `ClientRpcHost`.
- [ ] `ClientDisconnectSystem` subscribes to `ClientRpcHost.RemoteDisconnected`
      and keeps the character save, cleanup, toast, and menu transition.
- [ ] `NetworkStatsOverlay` reads `ClientRpcHost.Stats`.
- [ ] A loopback test covers list levels, join, play, leave, and rejoin. A TCP
      test covers the same path over `127.0.0.1:0`.
- [ ] A mid-join disconnect test proves the client returns to the menu cleanly.
- [ ] The docs pass updates `networking-overview`, `networking-join`,
      `networking-replication`, and `networking-transports`.

## Dependencies

- 0046 adds the protocol layer, services, fragments, and loopback transport.
- 0047 adds the server handlers and joins.
- 0049 fixes the loopback restore path.
