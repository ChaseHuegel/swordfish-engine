# Issue Index

One subject: the live index of open, in-progress, and completed issues.
Read [specs/issues](../specs/issues.md) before adding or changing an issue.

Two top-level sections hold the work: [Active Sprint](#active-sprint) and
[Backlog](#backlog). Each has three type subsections: Bug, Improvement, and
Research.

Each issue appears as a checkbox bullet linking to its own document. A pending
issue is unchecked (`- [ ]`). A done issue is checked (`- [x]`). A done issue
stays in its subsection, checked in place. Sprint rotation is user-managed and
is not part of this workflow.

## Active Sprint

### Bug

### Improvement

### Research

## Backlog

### Bug

- [ ] [Server accepts client-addressed snapshots for any entity](/docs/issues/0001-inbound-snapshot-injection.md) — cross-client command injection via forged input/interaction snapshots
- [ ] [Unsafe frame ingestion](/docs/issues/0002-unsafe-frame-ingestion.md) — unbounded frame lengths, nsdc deserializer OOB reads, client-caused tick aborts
- [ ] [Send-queue drop policy discards protocol-critical frames](/docs/issues/0003-send-queue-drop-policy.md) — silent data loss on the single FIFO; reliable-priority queue never evicts
- [ ] [World-stream integrity and join](/docs/issues/0004-world-stream-integrity.md) — stream truncation risk, no join timeout, infinite Loading; server backlog disconnect policy
- [ ] [Blocking, timeout-less TCP connect](/docs/issues/0005-blocking-connect.md) — synchronous connect hangs the client on unreachable hosts
- [ ] [Hub receive poll lets one client starve the rest](/docs/issues/0006-hub-receive-starvation.md) — per-client drain unbounded; needs fair MaxReceiveWindow rotation
- [ ] [Loading-state snapshot pile-up](/docs/issues/0007-loading-snapshot-pileup.md) — memory spike and join-time hitch from queued snapshots during stream
- [ ] [Transport lifecycle leaks](/docs/issues/0009-transport-lifecycle-leaks.md) — dead peers never disposed, host client registry never pruned, stale IsConnected
- [ ] [Interaction edges dropped on congestion](/docs/issues/0010-interaction-edge-loss.md) — unconditional outbound clear discards staged edges on failed send
- [ ] [Client prediction does not perform the documented replay](/docs/issues/0011-prediction-replay.md) — PendingInputComponent written and trimmed but never replayed

### Improvement

- [ ] [Replication hot-path allocations and scans](/docs/issues/0012-replication-hot-path.md) — per-entity registry list allocs, client full-store scan, per-check store locks
- [ ] [Per-tick snapshot serialized once per client](/docs/issues/0013-per-client-serialization.md) — fan-out the shared authoritative snapshot instead of N full serializations
- [ ] [Message serialization and transport copies unpooled](/docs/issues/0014-serialization-pooling.md) — ArrayPool, cached type tags, receive-path double copy
- [ ] [Component-level removal is not replicated](/docs/issues/0015-component-removal-replication.md) — store Remove<T> surface, ComponentRemoval wire delta, engine-first commit
- [ ] [Remove the legacy NATS/Torches networking path](/docs/issues/0016-remove-legacy-nats.md) — dead PacketStreamClient/ProtocolV1 no-op and Server.Core codegen remnants
- [ ] [NetworkRegistry registration failures are silent or deferred](/docs/issues/0017-registry-registration-failures.md) — Result-typed registration, fail-fast for built-ins, log-and-continue for mods
- [ ] [Networking docs drift](/docs/issues/0018-networking-docs-drift.md) — specs claim replay, QueryDirty model, and full-protocol loopback that the code does not have

### Research

- [ ] [Multiple ECS worlds — isolated per-level server worlds](/docs/issues/0008-multi-world-server.md) — proposal for per-world state/networking isolation, DI changes, idle unload
- [ ] [Thread-per-peer transport scaling](/docs/issues/0019-thread-per-peer-scaling.md) — 3N+1 network threads per host; measure and pick async multiplexing or a documented cap