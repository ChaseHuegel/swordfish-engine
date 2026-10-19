# Proposal: nsd-rpc 0.3.0 transport for real-time traffic

- Type: improvement
- Status: landed in nsd-rpc 0.3.0
- Target release: 0.3.0
- Wire protocol: unchanged, version 1
- Owner: nsd-rpc
- Source repository: `ChaseHuegel/nsd-rpc`

## Purpose

This document is the handoff proposal for the nsd-rpc 0.3.0 transport work.
It records the gaps found by a real-time game consumer, the design decisions,
and a ready-to-paste issue set for the nsd-rpc repository.

This game repository does not modify nsd-rpc. Copy the issue documents below
into `docs/issues/` in nsd-rpc, add the index entries, and work them in
numeric order. Confirm the issue numbers against the live index before you
add them.

## Landed in 0.3.0

nsd-rpc 0.3.0 implements the proposal. The transport keeps one reliable send
queue. No send class is added. The final API differs from the request in these
places.

| Proposal | Landed |
|---|---|
| `RpcLog.Sink` | `RpcOptions.LogSink` with a `TraceEventType` level. |
| `RpcPeer.Connection` public | Not public. `RpcPeer.RemoteEndPoint`, `RpcPeer.Close()`, and `RpcPeer.Stats` are public. |
| `IOwnedFrameSink` public | Still internal. A transport wrapper that returns the original connection keeps the owned-frame path. |
| `RpcConnectionStats` with `DroppedFrames` | `Writes` and `RejectedFrames`. No `DroppedFrames`. |
| `LastDisconnectReason` | `Disconnected` carries `RpcDisconnectReason`. `Close` takes a reason. |
| Idle, backlog, and batching defaults | All are opt-in and default to zero. The consumer enables them. |
| Decode failure mapping | Landed as proposed. A chunk fails the call with `InvalidRequest`. |

The game has no blocker.

- It sets the opt-in policy values in `network.toml`.
- The server records disconnect reasons with a wrapping `IRpcServerTransport`
  that returns the original connection. This keeps the pooled send path.
- The client reads the reason from the connection event.
- `RpcPeer.Stats` supplies the overlay counters.
- Send batching measured 23 times faster in the library benchmark. The game
  enables it with a 16 ms interval.

## Problem

The TCP transport targets request and response traffic. A real-time game also
sends per-tick state and large world streams on the same connection.

The current transport has four gaps.

1. **No backlog policy.** `TcpConnection` keeps one bounded send queue and
   rejects a frame when the queue is full. A consumer cannot tell a transient
   spike from a dead peer. A consumer that wants real-time behavior is tempted
   to drop state, which is unsafe on a reliable ordered transport.
2. **No liveness deadline.** `WaitForDataAsync` awaits without a deadline. A
   half-open peer keeps its connection and buffers forever. The handshake has
   a deadline, but the receive loop does not.
3. **No observability.** There are no counters for frames, bytes, queue depth,
   or rejected sends. A host that wraps the connection to count traffic loses
   the pooled owned-frame path.
4. **No operational control.** Connect follows the OS retry schedule. The
   listener binds all interfaces. Frames are written one per socket write.

These are general needs for a real-time RPC transport. They are not specific
to one application.

## Why there are no send classes

An earlier draft proposed two send lanes, one reliable and one droppable. This
final proposal does not. A droppable lane is a UDP pattern. It is unsafe on a
TCP stream when the payload is a delta.

The consumer's snapshots are deltas. A snapshot carries removals, one-shot
component updates, and staged client commands. A later snapshot does not
repeat them. The only full sync happens at join. A dropped delta therefore
leaves a ghost entity, stale state, or a lost input permanently.

Standard practice is consistent.

| System | State transport | Loss policy |
|---|---|---|
| Quake 3, Source | Unreliable snapshots, delta against a client-acked baseline, server keeps snapshot history | A missing baseline triggers a full snapshot. Loss is recoverable. |
| Modern shooters | Unreliable delta snapshots with acked baselines and redundancy | Loss is recoverable by design. |
| Minecraft, most TCP games | Reliable ordered packets | No application drop. Slow clients are throttled or disconnected. |
| ENet, GameNetworkingSockets | Reliable control channel plus unreliable state channel | The unreliable channel is UDP. The reliable channel never drops. |
| QUIC, WebTransport | Reliable streams plus datagrams | Dropping exists only on the datagram side. |

The rule is simple. You may drop state only when a later message can repair
it. A delta stream over TCP with silent drops has no repair mechanism.

The transport therefore needs one reliable queue and a clear close policy. A
peer that cannot drain its backlog is disconnected. It reconnects and gets a
fresh full state.

## Goals

- Never drop an accepted frame.
- Close a peer that cannot drain its backlog.
- Detect an idle or half-open peer.
- Report why a connection closed.
- Report traffic and queue metrics.
- Bound connect time and choose the listen address.
- Batch frames into one write.
- Expose the seams a host needs.
- Map every payload decode failure to `InvalidParams`.

## Non-goals

- No wire protocol change. The envelope stays version 1.
- No unreliable or droppable send class on TCP.
- No generic payload fragmentation.
- No broadcast API.
- No compression, encryption, or authentication.
- No new transport.
- No change to correlation, streaming, or cancellation semantics.

## Compatibility

- 0.3.0 is a pre-1.0 minor release.
- The wire format is unchanged. A 0.2.0 peer can talk to a 0.3.0 peer when
  neither uses a new API. Add an interop test.
- `IClientConnection` keeps its current shape. `ConnectTimeout` is a
  `TcpOptions` value.
- The new API is additive. `RpcContext.TryStream`, `IRpcConnectionStats`,
  `RpcPeer.Connection`, `RpcLog.Sink`, and the new `TcpOptions` keys do not
  change existing signatures.
- `RpcMethodAttribute` does not change.
- Document every behavior change in the release notes.

## Cross-cutting design rules

- One reliable send queue per connection.
- Queue sizes are bytes. Include the 4-byte length prefix.
- `SendQueueBytes` is a hard safety cap. A send at the cap fails.
- `SendBacklogDisconnectBytes` is a liveness threshold. It must not exceed the
  hard cap.
- A send failure is a peer failure. Close the peer. Do not silently drop the
  frame.
- `Packets` means frames. `Writes` means socket writes.
- No new hardcoded limit. Every size, interval, and deadline is a config key
  with a docs table entry.
- No test sleeps. Use short configured deadlines and deterministic signals.
- Keep zero runtime dependencies.
- No timer per connection. Use the monotonic timestamp source and check on
  enqueue and on drain.

## Proposed issue set

| Issue | Title | Type |
|---|---|---|
| 0056 | Reliable send queue with a backlog policy | Improvement |
| 0057 | Idle timeout and disconnect reasons | Improvement |
| 0058 | Connection metrics | Improvement |
| 0059 | Connect timeout and bind address | Improvement |
| 0060 | Send batching | Improvement |
| 0061 | Public extension seams and log sink | Improvement |
| 0062 | Map every payload decode failure to InvalidParams | Bug |

Recommended order: 0056, 0057, 0058, 0059, 0060, 0061, 0062.

All seven issues are done in nsd-rpc 0.3.0. The documents below are the
original handoff request. Use the "Landed in 0.3.0" section for the final API.

---

## docs/issues/0056-send-backlog-policy.md

```markdown
# Improvement: Reliable send queue with a backlog policy

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

`TcpConnection` has one bounded send queue. A full queue rejects the frame
([TcpConnection.cs:78](../../NsdRpc.Tcp/TcpConnection.cs:78)). The transport
has no stated policy for a peer that cannot drain its traffic.

A consumer that wants real-time behavior is tempted to drop state. That is
unsafe on a reliable ordered transport. A dropped delta loses removals,
one-shot component updates, and staged client commands. A later frame does
not repair them. Recovery requires a full state, a baseline, or a keyframe,
and the protocol has none of those.

The transport needs a clear reliable policy. Never drop an accepted frame.
Close a peer that cannot drain its backlog.

## Acceptance criteria

- [ ] The transport has one reliable send queue. There is no send class and no
      priority lane. No accepted frame is dropped.
- [ ] `SendQueueBytes` remains a hard safety cap. A send at the cap fails.
      The caller treats the failure as a peer failure.
- [ ] `TcpOptions.SendBacklogDisconnectBytes` and
      `TcpOptions.SendBacklogDisconnectMs` exist. Defaults appear in a docs
      table. The byte threshold must not exceed `SendQueueBytes`.
- [ ] If the queued bytes stay at or above the threshold for the window, the
      connection closes with `BacklogLimit`.
- [ ] The backlog check runs on enqueue and on drain. It uses the monotonic
      timestamp source. There is no timer per connection.
- [ ] `RpcContext.TryStream<TChunk>(in TChunk chunk)` returns false when the
      send is rejected. `RpcContext.Stream` keeps its assert behavior.
      `rpc-api` states the difference.
- [ ] A test proves a transient spike that drains inside the window keeps
      the connection open.
- [ ] A test proves a sustained backlog closes the connection with
      `BacklogLimit`.
- [ ] A test proves an accepted reliable frame is never dropped.
- [ ] A test proves a rejected `TryStream` is observable to the handler.
- [ ] No test sleeps. Use a short configured window and a deterministic
      signal.
- [ ] The docs pass updates the TCP options table, `rpc-api`, and
      `quick-reference`.
- [ ] No wire change.

## Implementation notes

- The threshold must not exceed the hard cap. The cap protects memory. The
  threshold protects liveness.
- Pacing is the producer's responsibility. A large stream must pace itself or
  use `TryStream`. The library does not silently drop a chunk.
- A future UDP transport can add unreliable classes. That is out of scope
  here.
```

---

## docs/issues/0057-idle-timeout-and-reasons.md

```markdown
# Improvement: Idle timeout and disconnect reasons

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

`TcpConnection.WaitForDataAsync` awaits the socket without a deadline
([TcpConnection.cs:259](../../NsdRpc.Tcp/TcpConnection.cs:259)). A peer that
disappears without a FIN keeps the connection and its buffers forever.

`Disconnected` is `Action` with no reason
([IRpcConnection.cs](../../NsdRpc/Transport/IRpcConnection.cs)). A host cannot
tell an idle timeout from a write failure, a backlog drop, or an intentional
close.

The handshake has a deadline. The receive loop does not.

## Acceptance criteria

- [ ] `RpcDisconnectReason` has `PeerClosed`, `ReadError`, `WriteError`,
      `IdleTimeout`, `HandshakeFailed`, `BacklogLimit`, `LocalClose`, and
      `ProtocolViolation`.
- [ ] `TcpOptions.IdleTimeout` defaults to 5 seconds. Zero or negative
      disables it. The key appears in the docs table.
- [ ] The receive loop arms a deadline for each wait. A received byte resets
      it.
- [ ] On expiry, the connection closes and fires `Disconnected` once with
      `IdleTimeout`.
- [ ] `LastDisconnectReason` is set before the event fires. It is readable
      after the event.
- [ ] Reasons are correct for peer EOF, read failure, write failure, idle
      timeout, handshake failure, backlog limit, local close, and decoder
      rejection.
- [ ] A test proves a silent peer closes after the deadline.
- [ ] A test proves a peer that sends one byte inside the deadline stays
      connected.
- [ ] A test proves the reason for EOF, local close, and idle timeout.
- [ ] No test sleeps. Use a short configured deadline and a deterministic
      signal.
- [ ] The docs pass updates the TCP options table and `rpc-api`.
- [ ] No wire change.

## Implementation notes

- Use a linked `CancellationTokenSource` with `CancelAfter` for each wait.
  This matches the handshake pattern at
  [TcpConnection.cs:137](../../NsdRpc.Tcp/TcpConnection.cs:137).
- The timeout measures received bytes, not sent bytes. A send-only consumer
  must send keepalives or disable the key. Document this rule.
- A handshake failure happens before the peer exists. Set the reason on the
  connection before it closes.
```

---

## docs/issues/0058-connection-metrics.md

```markdown
# Improvement: Connection metrics

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

The transport exposes no traffic counters and no queue depth. A host that
wraps `IRpcConnection` to count frames loses the pooled owned-frame path.

A host needs these values for an overlay and for load decisions.

- Frames and bytes in each direction.
- Queued bytes and the peak queue depth.
- Rejected sends.
- Frames dropped by protocol checks.

## Acceptance criteria

- [ ] `RpcConnectionStats` is a readonly struct with `PacketsSent`,
      `PacketsReceived`, `BytesSent`, `BytesReceived`, `QueuedBytes`,
      `PeakQueuedBytes`, `RejectedFrames`, and `DroppedFrames`.
- [ ] `IRpcConnectionStats` is an optional capability with
      `RpcConnectionStats Stats { get; }`.
- [ ] `TcpConnection` implements it. The counters use `Interlocked`.
- [ ] `BytesSent` and `BytesReceived` include the 4-byte length prefix.
      `Packets` counts frames, not writes.
- [ ] `QueuedBytes` is the current outbound queue size. `PeakQueuedBytes` is
      the high-water mark since construction.
- [ ] `RejectedFrames` counts refused sends. `DroppedFrames` counts frames
      dropped by a protocol check.
- [ ] `RpcPeer.Stats` returns the connection's stats. It returns defaults for a
      connection without the capability.
- [ ] A test pins exact counts for a known exchange.
- [ ] A test pins queued bytes and the peak under a stalled read.
- [ ] A concurrent test proves no lost counts.
- [ ] The docs pass updates `quick-reference` and `rpc-api`.
- [ ] No wire change.

## Implementation notes

- Update counters once per frame at enqueue and dequeue. Do not update per
  byte.
- Do not count a frame twice when batching. `Packets` counts frames.
- The consumer expects packet and byte totals that match the peer's opposite
  counters. Include the prefix on both sides.
```

---

## docs/issues/0059-connect-timeout-and-bind-address.md

```markdown
# Improvement: Connect timeout and bind address

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

`TcpClientConnection.ConnectAsync` uses `Socket.ConnectAsync(host, port)`
without a deadline ([TcpClientConnection.cs:59](../../NsdRpc.Tcp/TcpClientConnection.cs:59)).
A blackholed route waits out the OS retry schedule.

`TcpServerTransport.EnsureListener` binds `IPAddress.Any`
([TcpServerTransport.cs:192](../../NsdRpc.Tcp/TcpServerTransport.cs:192)).
A host cannot restrict the listener to one interface.

## Acceptance criteria

- [ ] `TcpOptions.ConnectTimeout` defaults to 10 seconds. Zero or negative
      disables it. The key appears in the docs table.
- [ ] `ConnectAsync` honors the deadline. On expiry, it disposes the socket
      and throws `TimeoutException` with the endpoint.
- [ ] `TcpOptions.BindAddress` defaults to `IPAddress.Any`. A null value
      fails fast.
- [ ] `TcpServerTransport` uses the bind address. `Port` still reports the
      bound port.
- [ ] A test proves a blackholed connect times out within the budget.
- [ ] A test proves a listener bound to loopback accepts a loopback client.
- [ ] A test proves `Port` works with a non-default bind address.
- [ ] The docs pass updates the TCP options table and `rpc-api`.
- [ ] No wire change.

## Implementation notes

- Keep the existing parameterless `ConnectAsync` signature. The timeout is a
  `TcpOptions` value.
- Validate the address in the constructor or at connect time. Do not defer a
  null failure to the socket.
- A later token overload can link the caller token to the deadline.
```

---

## docs/issues/0060-send-batching.md

```markdown
# Improvement: Send batching

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

`DrainSendQueueAsync` writes one frame per socket write
([TcpConnection.cs:165](../../NsdRpc.Tcp/TcpConnection.cs:165)). A world stream
sends hundreds of small frames. A per-tick publisher sends one frame per
peer per interval. Each frame costs one syscall and one small segment.

A consumer coalesces frames today to stay efficient. The transport should
offer that policy.

## Acceptance criteria

- [ ] `TcpOptions.SendBatchInterval` defaults to zero. Zero sends
      immediately.
- [ ] `TcpOptions.SendBatchMaxBytes` defaults to 65,536.
- [ ] The send loop drains queued frames into one pooled buffer, up to the
      byte limit, and writes once.
- [ ] A frame is never split. A frame larger than the limit is written alone.
- [ ] The interval coalesces frames that arrive during the wait. A send
      signal wakes the loop early.
- [ ] `Packets` counts frames correctly. Add a `Writes` counter if useful,
      and document the difference.
- [ ] A test proves N frames produce one write at a nonzero interval.
- [ ] A test proves the byte limit is respected.
- [ ] A test proves a large frame is sent alone.
- [ ] A benchmark records syscall count and throughput before and after.
- [ ] The docs pass updates the TCP options table and `performance`.
- [ ] No wire change.

## Implementation notes

- Default zero keeps the 0.2.0 behavior. A consumer opts into an interval.
- Do not hold the send lock during the socket write. Take frames, release
  the lock, then write.
- Return every pooled buffer on every path, including a write failure.
- A single frame larger than `SendBatchMaxBytes` must not deadlock the
  drain. Write it alone.
```

---

## docs/issues/0061-public-extension-seams.md

```markdown
# Improvement: Public extension seams and log sink

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

A host cannot instrument or adapt the transport without losing behavior.

- `IOwnedFrameSink` is internal. A public `IRpcConnection` decorator cannot
  preserve the pooled send path.
- `RpcPeer.Connection` is internal
  ([RpcPeer.cs:62](../../NsdRpc/Engine/RpcPeer.cs:62)). A host cannot read the
  remote endpoint or attach metadata from the connect event.
- `RpcLog` is internal and has no consumer hook
  ([RpcLog.cs](../../NsdRpc/Engine/RpcLog.cs)). A host cannot route drop and
  error events to its logger.

## Acceptance criteria

- [ ] `IOwnedFrameSink` is public, with XML docs and a stated ownership
      contract.
- [ ] `RpcPeer.Connection` is public. `RpcPeer.RemoteEndPoint` returns the
      server endpoint or null.
- [ ] `RpcLog.Sink` accepts a delegate with a level, a message, and an
      optional exception. The default remains `TraceSource`.
- [ ] No `Microsoft.Extensions.Logging` dependency.
- [ ] A test proves a public `IRpcConnection` decorator can forward
      `TrySendOwned`.
- [ ] A test proves a custom sink receives drop and error events.
- [ ] A test proves `RpcPeer.Connection` and `RemoteEndPoint` are available
      on the client and the server.
- [ ] The docs pass updates `rpc-api` and `quick-reference`.
- [ ] No wire change.

## Implementation notes

- Ownership contract: the receiver owns the frame and returns it to the pool
  on every path. A decorator that accepts the frame must return it.
- Keep the API minimal. Do not add a middleware pipeline.
- A sink that throws must not kill the transport thread. Catch and drop the
  sink failure.
```

---

## docs/issues/0062-decode-failure-mapping.md

```markdown
# Bug: Map every payload decode failure to InvalidParams

- Type: bug
- Status: open
- Workflow: ../specs/issues.md

## Problem

`NsdPayloadSerializer.TryDeserialize` catches only `InvalidDataException`
([NsdPayloadSerializer.cs:83](../../NsdRpc/Engine/NsdPayloadSerializer.cs:83)).
An nsd unpack can throw other exceptions on a malformed payload. The
dispatcher then catches the exception as a handler failure and answers
`InternalError`.

A corrupt request looks like a handler bug. A corrupt notification logs as a
handler error. A corrupt chunk follows the wrong path.

## Acceptance criteria

- [ ] Any exception from the payload deserializer maps to a decode failure.
- [ ] A request payload failure answers `InvalidParams`.
- [ ] A notification payload failure is dropped and logged.
- [ ] A chunk payload failure follows the documented stream rule. State the
      rule in `rpc-api` and test it.
- [ ] The connection stays open.
- [ ] The wire carries no exception detail.
- [ ] Tests cover a truncated payload, a length that exceeds the buffer, and a
      valid frame with a corrupt payload.
- [ ] The docs pass updates `rpc-api` and the `rpc-protocol` error table.
- [ ] No wire change.

## Implementation notes

- Do not catch `OutOfMemoryException`, `StackOverflowException`, or
  `ThreadAbortException`. Catch the decode boundary only.
- The library must not depend on a specific nsdc exception type.
- Coordinate with the downstream nsdc unpack safety work. The library stays
  correct even when the generated reader throws an index error.
```

---

## Index entries for nsd-rpc

Add to `docs/issues/index.md` in the nsd-rpc repository.

```markdown
## Active Sprint

### Bug

- [ ] [Map every payload decode failure to InvalidParams](/docs/issues/0062-decode-failure-mapping.md) — a corrupt payload must not look like a handler failure.

### Improvement

- [ ] [Reliable send queue with a backlog policy](/docs/issues/0056-send-backlog-policy.md) — never drop an accepted frame, close a peer that cannot drain.
- [ ] [Idle timeout and disconnect reasons](/docs/issues/0057-idle-timeout-and-reasons.md) — detect a half-open peer and report why it closed.
- [ ] [Connection metrics](/docs/issues/0058-connection-metrics.md) — frames, bytes, queue depth, and rejected sends.
- [ ] [Connect timeout and bind address](/docs/issues/0059-connect-timeout-and-bind-address.md) — bound connect and choose the listen interface.
- [ ] [Send batching](/docs/issues/0060-send-batching.md) — one write per interval instead of one write per frame.
- [ ] [Public extension seams and log sink](/docs/issues/0061-public-extension-seams.md) — a decorator keeps the pooled path, and a host can attach a logger.
```

## Release 0.3.0 checklist

Status: complete. nsd-rpc 0.3.0 is published.

1. Bump `Version` to `0.3.0` in `NsdRpc/NsdRpc.csproj`,
   `NsdRpc.Tcp/NsdRpc.Tcp.csproj`, and
   `NsdRpc.Generators/NsdRpc.Generators.csproj`.
2. Update `docs/specs/rpc-api.md`, `docs/specs/rpc-protocol.md`,
   `docs/quick-reference.md`, and `docs/performance.md`.
3. Add the new keys to the TCP options table.
4. Run `dotnet build` with zero warnings and `dotnet test`.
5. Run the benchmarks and record the batching result.
6. Add an interop test that proves a 0.2.0 peer can exchange frames with a
   0.3.0 peer when no new API is used.
7. Tag `v0.3.0`. The publish workflow packs and publishes all three packages.
8. Release notes: backlog policy, idle timeout, disconnect reasons, metrics,
   connect and bind, batching, public seams, decode mapping. State that the
   wire format is unchanged and there is no send class.

## Consumer validation

The first consumer is a multiplayer game with these needs. Use them to
validate the API.

| Need | API |
|---|---|
| No silent state loss | One reliable queue |
| Slow peer fails cleanly | `SendBacklogDisconnectBytes`, `SendBacklogDisconnectMs`, `BacklogLimit` reason |
| Bounded memory | `SendQueueBytes` hard cap |
| Pacing a large stream | `QueuedBytes`, `PeakQueuedBytes`, `TryStream` |
| Send coalescing | `SendBatchInterval`, `SendBatchMaxBytes` |
| Half-open detection | `IdleTimeout` |
| Observability | `IRpcConnectionStats` |
| Custom loopback transport | `IRpcConnection`, `IRpcServerTransport`, `IRpcConnectionStats` |
| Host logging | `RpcLog.Sink` |

The game paces the join stream with a byte budget per server tick. It checks
`QueuedBytes` and stops when the queue is above the watermark. It treats a
rejected send as a peer failure.

## Open questions

These questions are resolved by the 0.3.0 release.

1. `WaitForSendCapacityAsync` was not added. The consumer paces with
   `QueuedBytes` and `TryStream`. No follow-up is needed for the game.
2. The backlog close uses bytes. The release validates the threshold against
   `SendQueueBytes`.
3. `DroppedFrames` was not added. `RejectedFrames` covers refused sends. The
   game does not need a drop counter.
4. One hard `SendQueueBytes` cap is enough. The game paces the join stream and
   keeps the queue below the backlog threshold.

## Handoff steps

Status: complete. The seven issues landed in 0.3.0. The steps below remain as
the handoff record.

1. Copy the seven issue documents into `docs/issues/` in nsd-rpc. Confirm the
   numbers against the live index.
2. Add the index entries under Active Sprint. Adjust the section if the
   sprint is user-managed.
3. Work the issues in order. Run the docs pass on each.
4. Release 0.3.0. Notify the game branch so it can pin the package.
