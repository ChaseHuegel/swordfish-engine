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
- **Join-stream gating.** Once a client's world stream is enqueued
  (`ServerJoinSystem.BeginStream`), the publish stage sends it no per-tick
  deltas until the stream complete has been enqueued (`EndStream`), so a
  Loading-time client cannot accumulate unapplied snapshots. The one-shot
  full-sync publish is exempt and stays the client's first snapshot.
  Client-side, `ClientReconcileSystem` additionally coalesces any queued
  burst to the newest frame on entering `Playing`.
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

## Dirty tracking

Store-mediated writes (`Alloc<T...>`, `AddOrUpdate`, `Entity.Add`) auto-mark a
component dirty. In-place mutation through a query `ref` is NOT auto-detected —
mutating systems must use `store.QueryRef<T...>(...)` and go through the
`Ref<T>` accessor's `Write` property, which marks dirty and returns a write
`ref`. `QueryDirty<T>`/`QueryDirty<T1,T2>`/`QueryRemoved<T>` iterate matching
dirty components without clearing; callers must explicitly `ClearDirty<T>`.

## Tick tagging

`TickNumber` is the canonical sim tick (physics-step ordinal), not the server's
per-world replication tick. Input is staged per sim tick and consumed exactly
once per step. See [prediction](networking-prediction.md).

## Source of truth

- `WaywardBeyond.Server.Core/Systems/NetworkReplicationSystem.cs`
- `WaywardBeyond.Client.Core/Systems/ClientReplicationSystem.cs`
- `WaywardBeyond.Shared.Networking/Components/NetworkComponent.cs`

## Tests that pin this

- `Swordfish.Tests` interaction-staging tests (consume-per-sim-tick,
  newest-per-tick collapse, sequence dedupe).
- `Swordfish.Tests/SessionRoutingTests.cs` (per-client acks across N clients).