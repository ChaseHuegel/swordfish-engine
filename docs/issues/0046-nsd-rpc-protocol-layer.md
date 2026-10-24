# Improvement: Add the nsd-rpc protocol layer and loopback transport

- Type: improvement
- Status: done
- Workflow: ../specs/issues.md

## Problem

The game owns its own wire envelope, type-tag framing, serializer cache,
per-type receive queues, and FIFO request correlation. `LevelsClient` matches
responses to requests by response type and order. The join stream uses a
separate marker message. These are all work that an RPC layer provides.

nsd-rpc 0.3.0 provides the envelope, correlation ids, notifications, streaming,
cancellation, and service code generation. This issue adds the shared protocol
layer and the in-process loopback transport. It does not cut over the live
client or server.

## Acceptance criteria

- [x] `WaywardBeyond.Networking` references `NsdRpc`, `NsdRpc.Tcp`, and
      `NsdRpc.Generators` at 0.3.0. The generator is an analyzer.
- [x] `GameRpc` holds every method id in one constants class. Ids are grouped
      by subject and documented as a wire contract.
- [x] `GameRpcErrors` holds the app error codes in the `-32100` range.
- [x] `WaywardBeyond.Data/Nsd/network.nsd` adds `SnapshotBatch`, `LevelStreamChunk`, and
      `StreamLevelRequest`. `LevelEntityAdd` stays until the stream cutover.
- [x] `IGameServerApi` declares every client-to-server call, notification, and
      stream. `IGameClientApi` declares every server-to-client notification.
      Every method sets its direction.
- [x] `IGameServerApi` declares `JoinAsync` and `StreamLevelAsync` with
      `Streams = typeof(LevelStreamChunk)`. The generator cross-checks the
      chunk type.
- [x] A registry freeze test proves that no method id is zero, duplicated, or
      registered in the wrong direction.
- [x] A fragment helper splits and joins byte payloads with a 60,000-byte
      budget. It covers a zero-length payload, an exact boundary, one byte
      over the boundary, and a stale incomplete group.
- [x] `LoopbackRpcServerTransport`, `LoopbackRpcServerConnection`, and
      `LoopbackRpcClientConnection` implement the nsd-rpc transport
      interfaces, including `Close(RpcDisconnectReason)` and
      `event Action<RpcDisconnectReason> Disconnected`. Frames are copied,
      counters implement `IRpcConnectionStats`, and `Disconnected` fires once.
- [x] The loopback endpoint buffers outbound frames until the peer subscribes
      to `FrameReceived`. A hello sent before the server peer is wired is not
      lost.
- [x] The loopback server transport accepts a new client connection after the
      previous one closes. The client host can restore the loopback session
      after a remote disconnect.
- [x] `RpcOptions.LogSink` forwards nsd-rpc drop and error events to the game
      logger. `TraceEventType` maps to the engine log levels.
      `Transport.TraceLogging` still gates per-frame game logs.
- [x] `docs/specs/networking-rpc.md` records the service catalogue, method
      ids, error codes, fragment rule, and loopback behavior.
- [x] `dotnet build` succeeds with zero warnings.

## Method id ranges

| Range | Subject |
|---|---|
| 1–19 | connection: hello, leave, client heartbeat |
| 20–59 | replication: client snapshot |
| 60–99 | gameplay commands: chat |
| 100–149 | menu: list, create, delete, save |
| 200–249 | join: join, stream level |
| 300–349 | server state: snapshot, snapshot batch |
| 350–399 | server events: voxel edit, notification, skill, chat relay, heartbeat |

App error codes start at `-32100`: `NotJoined`, `JoinRefused`,
`LevelNotFound`, `LevelLoadFailed`, `PermissionDenied`, `InvalidState`, and
`StreamFailed`.

## Dependencies

- nsd-rpc 0.3.0 must be published. See
  [the proposal](../proposals/nsd-rpc-0.3.0-transport.md).
- The live client and server keep using the legacy transport until 0047 and
  0048.
