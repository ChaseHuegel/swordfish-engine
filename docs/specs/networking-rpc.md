# Networking RPC

One subject: the nsd-rpc transport contract for game traffic. It records the service
catalogue, method ids, app error codes, the fragment rule, and the loopback
behavior. The migration status lives in the active sprint issues. Read
[networking-overview](networking-overview.md) for the process boundary and
[networking-transports](networking-transports.md) for the transport policy.

## Packages

Source of truth: `WaywardBeyond.Networking/WaywardBeyond.Networking.csproj`.

The game references `NsdRpc`, `NsdRpc.Tcp`, and `NsdRpc.Generators` at 0.3.0.
The generator is an analyzer. The 0.3.0 package ships its assembly under
`lib/`, so the project adds it with an explicit `Analyzer` item.

No game code writes the envelope, the correlation table, or the frame format.
`NsdRpc` owns them. The game owns the service interfaces, the handlers, and the
in-process loopback transport.

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
| 20 | `ClientSnapshot` | client → server | notification | `WorldSnapshot` |
| 60 | `Chat` | client → server | notification | `ChatMessage` |
| 100 | `ListLevels` | client → server | call | `ListLevelsRequest` → `ListLevelsResponse` |
| 101 | `CreateLevel` | client → server | call | `NewLevelRequest` → `NewLevelResponse` |
| 102 | `DeleteLevel` | client → server | call | `DeleteLevelRequest` → `DeleteLevelResponse` |
| 103 | `SaveLevel` | client → server | call | `SaveLevelRequest` → `SaveLevelResponse` |
| 200 | `Join` | client → server | call | `JoinRequest` → `JoinAccept` |
| 201 | `StreamLevel` | client → server | stream | `StreamLevelRequest` → `LevelStreamChunk`* → `LevelStreamComplete` |
| 300 | `WorldSnapshot` | server → client | notification | `WorldSnapshot` |
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

`IGameClientApi` declares every server-to-client notification. The server uses
`GameClientApiClient` per peer. `ClientHandlers` derives from
`GameClientApiHandler`.

Every method sets `RpcDirection`. A call in the wrong direction answers
`MethodNotFound`. The registry is built once and frozen at startup.

## Error codes

Source of truth: `WaywardBeyond.Networking/Rpc/GameRpcErrors.cs`.

App codes start at `-32100` and go down. The wire carries the code and the
message, never exception detail.

| Code | Name | Meaning |
|---|---|---|
| -32100 | `NotJoined` | The peer has no session for the requested action. |
| -32101 | `JoinRefused` | The server refused the join. |
| -32102 | `LevelNotFound` | The requested level does not exist. |
| -32103 | `LevelLoadFailed` | The level exists but did not load. |
| -32104 | `PermissionDenied` | The peer lacks the required permission. |
| -32105 | `InvalidState` | The request is not valid in the current state. |
| -32106 | `StreamFailed` | The level stream aborted. |

## Fragment rule

Source of truth: `WaywardBeyond.Networking/Rpc/PayloadFragmenter.cs` and
`FragmentAssembler.cs`.

A payload larger than `GameRpc.FragmentBudget` (60,000 bytes) is split into
ordered parts. The budget is below the 65,570-byte default frame cap with
envelope headroom. A zero-length payload is one empty part.

`SnapshotBatch` carries `BatchId`, `PartIndex`, `PartCount`, and `Payload`.
`LevelStreamChunk` carries `PartIndex`, `PartCount`, and `Payload`.

Parts must arrive in order. A new batch id, a repeated first part, an
out-of-order part, or a changed part count drops the stale incomplete group. A
stale group never joins with a later group.

## Loopback transport

Source of truth: `WaywardBeyond.Networking/Transport/LoopbackRpc*.cs`.

The loopback carries whole RPC frames between two endpoints in one process. It
implements the nsd-rpc transport interfaces: `IRpcServerTransport`,
`IServerConnection`, and `IClientConnection`.

- `TrySend` copies the frame before it returns.
- A receiving endpoint buffers frames until its peer subscribes to
  `FrameReceived`. A hello sent before the server peer is wired is not lost.
- `Disconnected` fires once. The closing side passes its reason. The peer side
  sees `PeerClosed`.
- Each side reports its own counters through `IRpcConnectionStats`.
- The server transport stays open when a client closes. It accepts a fresh
  client connection with a new client id, so the client host can restore the
  loopback after a remote disconnect.

## Logging

Source of truth: `WaywardBeyond.Networking/Rpc/RpcLogging.cs`.

`RpcOptions.LogSink` receives every nsd-rpc drop and error event. The game maps
`TraceEventType` to the engine log levels. The sink is not gated by the
transport trace switch. `Transport.TraceLogging` still gates the per-frame game
logs.

## Threading

The nsd-rpc peer runs one dispatch pump per connection. Handlers run in arrival
order, off the caller's thread. Server and client handlers must not touch a
`DataStore`. They stage work and the owning ECS thread performs it. The
migration issues 0047 and 0048 apply this rule.
