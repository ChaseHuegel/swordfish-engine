# Improvement: Remove the legacy transport and remap network config

- Type: improvement
- Status: done
- Workflow: ../specs/issues.md

## Problem

After the client and server migrations, the legacy transport, serializers,
hub, FIFO correlation, and their config keys have no callers. Leaving them in
place keeps two networking models in the tree and lets new code take the wrong
path.

The discovery protocol version also identifies the old TCP protocol. An old
game client must not try to join a new game server.

## Acceptance criteria

- [x] Delete `Transport/TcpTransport.cs`, `TcpServerHost.cs`,
      `ServerConnectionHub.cs`, `LocalConnection.cs`, `INetworkTransport.cs`,
      `IClientConnection.cs`, `IServerConnection.cs`, `SendPriority.cs`, and
      `DisconnectReason.cs`.
- [x] Delete `Serialization/INetworkSerializer.cs`, `NsdMessageSerializer.cs`,
      and `SerializerCache.cs`.
- [x] Delete `ConnectionClaims.cs` and `PendingLevelDeletes.cs`. Remove the
      `PendingJoins` class if `ServerPeers` fully replaces it.
- [x] Remove `NetworkingComposition.RegisterTransportMessage` and every
      remaining call.
- [x] Remove the unused `LevelEntityAdd` message from `levels.nsd` and
      regenerate. `LevelStreamComplete` stays as the stream response.
- [x] No source file references `TcpTransport`, `ServerConnectionHub`,
      `IClientConnection`, `SendPriority`, or `NsdMessageSerializer`.
- [x] `network.toml` removes `TimeoutMs`, `MaxFrameBytes`, `SendIntervalMs`,
      `SendQueueSize`, `ReliableQueueConcernThreshold`,
      `ReliableQueueDisconnectThreshold`, `ReliableQueueDisconnectMs`, and
      `MaxReceiveWindow`.
- [x] `[Transport]` adds `ConnectTimeoutMs`, `IdleTimeoutMs`,
      `SendBatchIntervalMs`, `SendBatchMaxBytes`, `SendQueueBytes`,
      `SendBacklogDisconnectBytes`, and `SendBacklogDisconnectMs`.
- [x] `[Protocol]` adds `CallTimeoutMs`, `MaxPendingCalls`,
      `MaxBufferedChunks`, and `LevelStreamBytesPerTick`.
- [x] `NetworkingConfig` exposes each new key under the matching table. The
      values feed `RpcOptions` and `TcpOptions`.
- [x] The shipped `network.toml` enables the opt-in transport policies.
      `IdleTimeoutMs`, `SendBatchIntervalMs`, and both backlog keys are
      positive.
- [x] `SendBacklogDisconnectBytes` is not greater than `SendQueueBytes`.
      An invalid combination fails fast at startup.
- [x] `LanDiscovery.ProtocolVersion` increments so old clients are filtered.
- [x] The dedicated server `config/network.toml` matches the new schema.
- [x] `dotnet build` succeeds with zero warnings.
- [x] The docs pass updates the config table in `networking-transports` and
      `config-schemas`.

## Defaults

| Key | Value |
|---|---|
| `ConnectTimeoutMs` | 5000 |
| `IdleTimeoutMs` | 5000 |
| `SendBatchIntervalMs` | 16 |
| `SendBatchMaxBytes` | 65536 |
| `SendQueueBytes` | 4194304 |
| `SendBacklogDisconnectBytes` | 1048576 |
| `SendBacklogDisconnectMs` | 10000 |
| `CallTimeoutMs` | 30000 |
| `MaxPendingCalls` | 1024 |
| `MaxBufferedChunks` | 1024 |
| `LevelStreamBytesPerTick` | 262144 |

The library defaults disable the idle timeout, the backlog policy, and send
batching. The game enables all three.

## Dependencies

- 0047 and 0048 remove the last callers.
- 0049 restores the loopback through the new host.
