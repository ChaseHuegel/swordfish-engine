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

- [x] [Server accepts client-addressed snapshots for any entity](/docs/issues/0001-inbound-snapshot-injection.md) — cross-client command injection via forged input/interaction snapshots
- [x] [Unsafe frame ingestion](/docs/issues/0002-unsafe-frame-ingestion.md) — unbounded frame lengths, nsdc deserializer OOB reads, client-caused tick aborts
- [x] [Send-queue drop policy discards protocol-critical frames](/docs/issues/0003-send-queue-drop-policy.md) — silent data loss on the single FIFO; reliable-priority queue never evicts
- [x] [World-stream integrity and join](/docs/issues/0004-world-stream-integrity.md) — stream truncation risk, no join timeout, infinite Loading; server backlog disconnect policy
- [x] [Blocking, timeout-less TCP connect](/docs/issues/0005-blocking-connect.md) — synchronous connect hangs the client on unreachable hosts
- [x] [Hub receive poll lets one client starve the rest](/docs/issues/0006-hub-receive-starvation.md) — per-client drain unbounded; needs fair MaxReceiveWindow rotation
- [x] [Loading-state snapshot pile-up](/docs/issues/0007-loading-snapshot-pileup.md) — memory spike and join-time hitch from queued snapshots during stream
- [x] [Transport lifecycle leaks](/docs/issues/0009-transport-lifecycle-leaks.md) — dead peers never disposed, host client registry never pruned, stale IsConnected
- [x] [Interaction edges dropped on congestion](/docs/issues/0010-interaction-edge-loss.md) — unconditional outbound clear discards staged edges on failed send
- [x] [Client prediction does not perform the documented replay](/docs/issues/0011-prediction-replay.md) — PendingInputComponent written and trimmed but never replayed
- [x] [Disconnect handling has holes outside Playing](/docs/issues/0020-disconnect-state-machine.md) — save-screen disconnect never returns to menu, join hangs, in-flight world ops await forever
- [x] [Inventory moves don't replicate](/docs/issues/0021-inventory-replication.md) — InventoryEvent/SlotMoveOp op messaging architecture, shared resolver, authoritative echo
- [x] [Embedded NATS server process leaks on app close](/docs/issues/0022-nats-process-leak.md) — child never terminated, restart race on dispose, close path unverified
- [x] [Network logging — disconnect reasons and tracing](/docs/issues/0023-network-logging.md) — reason-carrying OnDisconnected, NetworkingSettings.TraceLogging gate
- [x] [Break/place sounds don't play for remote or authoritative edits](/docs/issues/0024-networked-audio.md) — silent authoritative apply path, no double-play on own echoes
- [x] [Name tag layer renders over the HUD](/docs/issues/0026-nametag-layer-order.md) — nameplate layer ordered above hotbar/inventory widgets
- [x] [Name tag distance — default 32 and a setting](/docs/issues/0027-nametag-distance-setting.md) — UISettings.NameplateDistance, 0-64 step 8 on the settings page
- [x] [Persist the last-entered server address and port](/docs/issues/0028-persist-last-server.md) — DefaultHost/DefaultConnectPort written to network.toml on join
- [x] [Continue button connects to the last joined server](/docs/issues/0029-continue-last-server.md) — ProfileSettings.LastServerMode marker branches local vs remote
- [x] [Saved server list on the multiplayer page](/docs/issues/0030-saved-servers.md) — ProfileSettings records, connect/remove with icon styling, 32-entry cap
- [x] [Multiplayer page — Enter submits connect](/docs/issues/0031-multiplayer-enter-submit.md) — keyboard submit on the address/port fields
- [x] [Session heartbeats — server TPS and client sim-tick reporting](/docs/issues/0032-session-heartbeats.md) — paired nsd heartbeats replace the transport keepalive, TickLagWarnThreshold
- [x] [F3 network statistics](/docs/issues/0033-f3-network-stats.md) — transport counters + engine Sampler rates + player count via heartbeat
- [x] [Bandwidth — snapshot-rate reduction and interval batching](/docs/issues/0034-snapshot-rate-and-batching.md) — SnapshotHz 30 default, SendIntervalMs coalesced drain, no traffic caps
- [x] [Headless dedicated server launcher](/docs/issues/0035-dedicated-server-launcher.md) — composition-driven (no mode flag), shared host composition in Server.Core

### Improvement

- [x] [Replication hot-path allocations and scans](/docs/issues/0012-replication-hot-path.md) — per-entity registry list allocs, client full-store scan, per-check store locks
- [ ] [Per-tick snapshot serialized once per client](/docs/issues/0013-per-client-serialization.md) — fan-out the shared authoritative snapshot instead of N full serializations
- [ ] [Message serialization and transport copies unpooled](/docs/issues/0014-serialization-pooling.md) — ArrayPool, cached type tags, receive-path double copy
- [x] [Component-level removal is not replicated](/docs/issues/0015-component-removal-replication.md) — store Remove<T> surface, ComponentRemoval wire delta, engine-first commit
- [x] [Remove the legacy packet-relay networking path](/docs/issues/0016-remove-legacy-nats.md) — dead PacketStreamClient/ProtocolV1 no-op and Server.Core codegen remnants; NATS persistence stays
- [x] [NetworkRegistry registration failures are silent or deferred](/docs/issues/0017-registry-registration-failures.md) — Result-typed registration, fail-fast for built-ins, log-and-continue for mods
- [ ] [Networking docs drift](/docs/issues/0018-networking-docs-drift.md) — specs claim replay, QueryDirty model, and full-protocol loopback that the code does not have

### Research

- [ ] [Multiple ECS worlds — isolated per-level server worlds](/docs/issues/0008-multi-world-server.md) — proposal for per-world state/networking isolation, DI changes, idle unload
- [ ] [Thread-per-peer transport scaling](/docs/issues/0019-thread-per-peer-scaling.md) — 3N+1 network threads per host; measure and pick async multiplexing or a documented cap
- [ ] [Validate LAN discovery broadcast/scan correctness](/docs/issues/0025-lan-discovery-validation.md) — standard networks only; fix defects or document as designed; no tailscale work
- [ ] [Persistence backend re-evaluation](/docs/issues/0036-nats-persistence-backend.md) — NATS for game saves vs sqlite for character saves; proposal in a new doc for user review

## Backlog

### Bug

### Improvement

- [ ] [nsdc unpack safety — submit B1-B3 upstream](/docs/issues/0037-nsdc-unpack-safety.md) — generated deserializer OOB reads; frame-level mitigations landed, structural fix needs codegen

### Research
