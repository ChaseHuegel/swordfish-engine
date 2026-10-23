# Networking RPC

One subject: the nsd-rpc contract for game traffic. It records the service
catalogue, method ids, app error codes, the send and backlog policy, the
fragment rule, the loopback behavior, and the threading model. Read
[networking-overview](networking-overview.md) for the process boundary and
[networking-transports](networking-transports.md) for the host wiring.

## Packages

Source of truth: `WaywardBeyond.Networking/WaywardBeyond.Networking.csproj`.

The game references `NsdRpc`, `NsdRpc.Tcp`, and `NsdRpc.Generators` at 0.3.0.
The generator is an analyzer. The 0.3.0 package ships its assembly under
`lib/`, so the project adds it with an explicit `Analyzer` item.

No game code writes the envelope, the frame format, the correlation table, or
the per-connection dispatch queue. The game owns the service interfaces, the
handlers, the peer directory, the loopback transport, and the host wiring.

## Framing

`NsdRpc` owns the transport frame: a 4-byte little-endian length prefix and the
nsd `Envelope`. The envelope carries the protocol version, message kind,
correlation id, method id, payload, and error. The default frame cap is 65,570
bytes. The game splits larger state payloads (see [Fragments](#fragments)).

## Method ids

Source of truth: `WaywardBeyond.Networking/Rpc/GameRpc.cs`.

A method id is a wire contract. Never change or reuse one between builds.

| Range | Subject |
|---|---|
| 1–19 | connection: hello, leave, client heartbeat |
| 20–59 | replication: client snapshot |
| 60–99 | gameplay commands: chat |
| 100–149 | menu: list, create, delete, save |
| 200–249 | join: join, stream level |
| 300–349 | server state: snapshot, snapshot batch |
| 350–399 | server events: voxel edit, notification, skill, chat relay, heartbeat |

| Id | Method | Direction | Kind | Payload |
|---|---|---|---|---|
| 1 | `ClientHello` | client → server | notification | `ClientHello` |
| 2 | `LeaveGame` | client → server | notification | `LeaveGameRequest` |
| 3 | `ClientHeartbeat` | client → server | notification | `ClientHeartbeatMessage` |
| 20 | `SendSnapshot` | client → server | notification | `WorldSnapshot` |
| 60 | `Chat` | client → server | notification | `ChatMessage` |
| 100 | `ListLevels` | client → server | call | `ListLevelsRequest` → `ListLevelsResponse` |
| 101 | `CreateLevel` | client → server | call | `NewLevelRequest` → `NewLevelResponse` |
| 102 | `DeleteLevel` | client → server | call | `DeleteLevelRequest` → `DeleteLevelResponse` |
| 103 | `SaveLevel` | client → server | call | `SaveLevelRequest` → `SaveLevelResponse` |
| 200 | `Join` | client → server | call | `JoinRequest` → `JoinAccept` |
| 201 | `StreamLevel` | client → server | stream | `StreamLevelRequest` → `LevelStreamChunk`* → `LevelStreamComplete` |
| 300 | `SendSnapshot` | server → client | notification | `WorldSnapshot` |
| 301 | `SnapshotBatch` | server → client | notification | `SnapshotBatch` |
| 350 | `VoxelEdit` | server → client | notification | `VoxelEditMessage` |
| 351 | `Notification` | server → client | notification | `NotificationMessage` |
| 352 | `SkillUpdate` | server → client | notification | `SkillStateUpdateMessage` |
| 353 | `ChatRelay` | server → client | notification | `ChatMessage` |
| 354 | `ServerHeartbeat` | server → client | notification | `ServerHeartbeatMessage` |

## Services

Source of truth: `WaywardBeyond.Networking/Rpc/`.

`IGameServerApi` declares every client-to-server invocation and handler shape.
`GameServerApiClient` is the client proxy. `ServerHandlers` derives from
`GameServerApiHandler`.

`IGameClientApi` declares every server-to-client notification. The server caches
one `GameClientApiClient` per peer in `ServerPeer.Client`. `ClientHandlers`
derives from `GameClientApiHandler`.

Both interfaces name the snapshot notifications `SendSnapshot`. The direction is
the contract: `SEND_SNAPSHOT` (20) uploads the client's components,
`SEND_WORLD_SNAPSHOT` (300) delivers the authoritative delta.

Every method sets `RpcDirection`. A call in the wrong direction answers
`MethodNotFound`. The registry is built once and frozen at startup.

## Error codes

Source of truth: `WaywardBeyond.Networking/Rpc/GameRpcErrors.cs`.

App codes start at `-32100` and go down. The wire carries the code and the
message, never exception detail.

| Code | Name | Meaning |
|---|---|---|
| -32100 | `NOT_JOINED` | The peer has no session for the requested action. |
| -32101 | `JOIN_REFUSED` | The server refused the join. |
| -32102 | `LEVEL_NOT_FOUND` | The requested level does not exist. |
| -32103 | `LEVEL_LOAD_FAILED` | The level exists but did not load. |
| -32104 | `PERMISSION_DENIED` | The peer lacks the required permission. |
| -32105 | `INVALID_STATE` | The request is not valid in the current state. |
| -32106 | `STREAM_FAILED` | The level stream aborted. |

## Send policy

nsd-rpc has one reliable send queue per connection. No frame is dropped after
the send path accepts it. A full queue rejects the send, and the caller treats
that as a peer failure. There is no send class, priority lane, or droppable
snapshot queue.

- State is a delta. A later frame does not repair a lost removal, one-shot
  component, interaction edge, or inventory op.
- A peer that cannot drain its backlog is closed with `BacklogLimit`. It
  reconnects and receives a fresh join and full state.
- `RpcPeer.Stats` reports frames, bytes, queued bytes, peak queued bytes,
  writes, and rejected sends.
- Send batching is opt-in. With `SendBatchIntervalMs` positive, the transport
  coalesces queued frames into one write, up to `SendBatchMaxBytes`. A frame
  larger than the limit is written alone.

## Backlog and idle policy

`network.toml` sets the policy. See
[transports](networking-transports.md#configuration).

- `SendQueueBytes` is the hard memory cap. A send at the cap fails.
- `SendBacklogDisconnectBytes` starts the backlog window. The value must not
  exceed `SendQueueBytes`.
- When queued bytes stay at or above the threshold for
  `SendBacklogDisconnectMs`, the connection closes with `BacklogLimit`.
- `IdleTimeoutMs` closes a connection when no bytes arrive in the window. The
  session heartbeat cadence is clamped below it, so a live peer always sends
  bytes.
- `ConnectTimeoutMs` bounds one TCP connect attempt.

## Fragments

Source of truth: `WaywardBeyond.Networking/Rpc/PayloadFragmenter.cs` and
`FragmentAssembler.cs`.

A payload larger than `PayloadFragmenter.FRAGMENT_BUDGET` (60,000 bytes) is split
into ordered parts. The budget is below the 65,570-byte frame cap with envelope
headroom. `NetworkingConfigExtensions.ToRpcOptions` pins `MaxFrameBytes` to that
cap. A zero-length payload is one empty part.

`SnapshotBatch` carries `BatchId`, `PartIndex`, `PartCount`, and `Payload`.
`LevelStreamChunk` carries `PartIndex`, `PartCount`, and the payload bytes. A
level chunk payload is a serialized `VoxelEntityData`, never a `LevelEntityAdd`
wrapper.

Parts must arrive in order. A new batch id, a repeated first part, an
out-of-order part, or a changed part count drops the stale incomplete group and
reports `FragmentState.Rejected`. A group over `MAX_PARTS` or
`MAX_PAYLOAD_BYTES` is rejected before it can allocate. A stale group never
joins with a later group.

## Join and level stream

The join is two correlated calls.

1. `JoinAsync` routes the peer to its world. The world seats the session and
   player mirror, binds the claim, opens the snapshot gate, and returns
   `JoinAccept`.
2. `StreamLevelAsync` runs on the server thread. It sends `LevelStreamChunk`
   fragments for each level entity, paced by `LevelStreamBytesPerTick` and by
   `RpcPeer.Stats.QueuedBytes`. It uses `RpcContext.TryStream` for each fragment
   and aborts when a send is rejected. The final result is
   `RpcResult<LevelStreamComplete>`.
3. `EndStream` lifts the snapshot gate after the stream completes. The one-shot
   full-sync snapshot is exempt and remains the client's first snapshot.

## Server peer directory

Source of truth: `WaywardBeyond.Server/ServerPeers.cs` and `ServerPeer.cs`.

`ServerPeer` holds the stable game `Uuid`, the `RpcPeer`, the cached
`GameClientApiClient`, the remote endpoint, the latest stats, the last
disconnect reason, `IsLocal`, the claim, the bound world, and the last inbound
stamp.

`ServerPeers` keys peers by game `Uuid` and by `(IRpcHost, clientId)`. Library
client ids from the loopback and LAN `RpcServer` instances cannot collide. A
removed peer is queued for teardown on the server thread.

## Client host and loopback

Source of truth: `WaywardBeyond.Client/Networking/ClientRpcHost.cs`,
`ClientInbound.cs`, and `ClientHandlers.cs`.

`ClientRpcHost` owns the frozen client registry, the active `RpcClient`, the
`GameServerApiClient`, the connection, the inbound queues, and the stats. It
connects the in-process loopback at startup and sends `ClientHello`.

`ConnectRemote` builds a `TcpClientConnection`, honors `ConnectTimeoutMs`, then
closes the loopback and activates the remote peer. A remote drop records the
reason, raises `RemoteDisconnected`, and restores a fresh loopback session.
See [transports](networking-transports.md#loopback).

`ClientInbound` holds one ordered snapshot stream (full snapshots and fragment
batches) plus per-kind queues for heartbeats, voxel edits, notifications, skill
updates, chat, and level chunks. It clears on every connection switch.

The loopback endpoint buffers frames until the peer subscribes to
`FrameReceived`. A hello sent before the server peer is wired is not lost.
`Disconnected` fires once. Each side reports its own counters.

## Threading

The nsd-rpc peer runs one dispatch pump per connection. Handlers run in arrival
order, off the caller's thread.

- `ServerHandlers` only reads the context and enqueues. No handler touches a
  `DataStore`.
- `ServerHostCommands` runs on the server thread. It routes joins and leaves,
  serves menu calls, and paces level streams. `ILevelCatalog` access stays
  single-threaded.
- In-world save calls stay in the world tick. `ServerSaveSystem` completes the
  caller's result on the server thread.
- `ClientHandlers` only enqueues. The client ECS thread drains
  `ClientInbound`.

## Logging

Source of truth: `WaywardBeyond.Networking/Rpc/RpcLogging.cs`.

`RpcOptions.LogSink` receives every nsd-rpc drop and error event. The game maps
`TraceEventType` to the engine log levels. The sink is not gated by a trace
switch.
