# Networking — Replication

One subject: dirty-driven replication between server and client.

## Server → client

`NetworkReplicationSystem` (`Server.Core/Systems/NetworkReplicationSystem.cs`):

- **Apply stage** drains `WorldSnapshot`s across **all** connected clients,
  binding every inbound `ClientOwned` snapshot to its sender's session entity:
  a wire uuid naming any other entity is ignored, never allocated, staged, or
  applied (`NetworkReplicationSystem.ApplyStage`). Accepted `InputComponent`s
  advance `LastAckedInput`/`LastAckedSnapshot`; input and interaction events
  are staged, not applied in place.
- **Publish stage** queries entities carrying `NetworkComponent`, serializes
  every dirty `ServerOwned` component into `ComponentSnapshot`s, then composes
  a **per-client** `WorldSnapshot` for every connected client. The component
  set and `RemovedEntities` are the same for all clients, but
  `LastProcessedInput` is that client's own entity's `LastAckedInput`.
- **Despawns** are recorded via `RequestDespawn` (must be called before the
  entity is freed, since `DataStore.Free` clears the uuid) and broadcast in
  `RemovedEntities`.
- **Publish cadence.** The publish stage emits once per
  `NetworkingSettings.SnapshotHz` (default 30 Hz), measured in wall-clock time
  from the tick deltas, not every tick. `TickNumber`/`LastProcessedInput`
  semantics are unchanged; despawns and the full-sync publish ride the same
  cadence, at most one interval of delay. The transport additionally coalesces
  pending frames into one socket write per `SendIntervalMs` (see
  [transports](networking-transports.md)).
- **Join-stream gating.** Once a client's world stream is enqueued
  (`ServerJoinSystem.BeginStream`), the publish stage sends it no per-tick
  deltas until the stream complete has been enqueued (`EndStream`), so a
  Loading-time client cannot accumulate unapplied snapshots. The one-shot
  full-sync publish is exempt and stays the client's first snapshot.
  Client-side, `ClientReconcileSystem` additionally coalesces any queued
  burst on entering `Playing`: the newest frame applies fully, and every
  superseded frame still has its non-motion components applied first, so
  one-shot state seeded early in the join burst (the starter inventory)
  survives a motion-superseding burst.
- **World/voxel bodies.** Server-authoritative structures carrying a
  `NetworkComponent` flow through this same path each tick. Their
  `TransformComponent`/`PhysicsComponent` are dirtied by the physics sync
  cycle, so the client snaps drift in its local prediction colliders.

See [voxel-edits](networking-voxel-edits.md) for authoritative voxel `content`
edits, which flow as `VoxelEditMessage` broadcasts rather than snapshots.

## Client → server

`ClientReplicationSystem` (`Client.Core/Systems/ClientReplicationSystem.cs`)
iterates **dirty `ClientOwned`** components — `InputComponent` and
`InteractionEvent` — and sends them up in a `WorldSnapshot`. Discrete
interaction taps are latched into a coalesced edge queue (mirroring the
`PendingInputComponent` ring buffer) so a tap falling in a send gap is still
delivered on the next packet, and inventory moves ride the same drain as
`InventoryEvent` ops (see [inventory](networking-inventory.md)). Outbound edges
and ops are cleared only after the containing snapshot is actually sent: a
failed send leaves them staged and the next successful tick re-emits them, so
edge and op delivery trades latency, never drops. Client-owned components that
are not packet-level (e.g. the active inventory slot) flow as dirty deltas,
where clearing is best-effort per tick.

Upload is paced to `NetworkingSettings.SnapshotHz` (default 30 Hz), **not** the
ECS tick rate. The system accumulates tick deltas and only collects and sends
once per interval. Dirty components persist across skipped ticks, and staged
edges and ops stay buffered, so the cadence trades latency, never delivery.

## Dirty tracking

Store-mediated writes (`Alloc<T...>`, `AddOrUpdate`, `Entity.Add`) auto-mark a
component dirty. In-place mutation through a query `ref` is NOT auto-detected —
mutating systems must use `store.QueryRef<T...>(...)` and go through the
`Ref<T>` accessor's `Write` property, which marks dirty and returns a write
`ref`. Replication iterates entities by component types, and each system caches
its direction's `NetworkRegistry` component list once per instance so the hot
path allocates nothing per entity; the client collection is player-scoped (only
the local player carries client-owned components). The full-state (join-time)
path still reads the store's boxed component span per entity — bounded by
joins, never the tick loop.

## Tick tagging

`TickNumber` is the canonical sim tick (physics-step ordinal), not the server's
per-world replication tick. Input is staged per sim tick and consumed exactly
once per step. See [prediction](networking-prediction.md).

## Source of truth

- `WaywardBeyond.Server/Systems/NetworkReplicationSystem.cs`
- `WaywardBeyond.Client/Systems/ClientReplicationSystem.cs`
- `WaywardBeyond.Networking/Components/NetworkComponent.cs`

## Tests that pin this

- `Swordfish.Tests` interaction-staging tests (consume-per-sim-tick,
  newest-per-tick collapse, sequence dedupe).
- `Swordfish.Tests/SessionRoutingTests.cs` (per-client acks across N clients).