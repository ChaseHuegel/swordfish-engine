# Networking — Replication

One subject: dirty-driven replication between server and client.

## Server → client

`NetworkReplicationSystem` (`WaywardBeyond.Server/Systems/NetworkReplicationSystem.cs`):

- **Apply stage** drains each bound peer's `ServerPeer.Snapshots` queue. Every
  inbound `ClientOwned` snapshot is bound to its sender's session entity: a wire
  uuid naming any other entity is ignored, never allocated, staged, or applied.
  Accepted `InputComponent`s advance `LastAckedInput`/`LastAckedSnapshot`.
  Input, interaction events, and inventory ops are staged, not applied in place.
  A malformed snapshot from one peer is logged and skipped.
- **Publish stage** queries entities carrying `NetworkComponent`, serializes
  every dirty `ServerOwned` component into `ComponentSnapshot`s, and composes a
  **per-client** `WorldSnapshot`. The component set and removals are shared, but
  `LastProcessedInput` is that client's own `LastAckedInput`.
- **Despawns** are recorded via `RequestDespawn` before the entity is freed
  (`DataStore.Free` clears the uuid) and broadcast in `RemovedEntities`.
- **Reliable send.** Each snapshot is a typed `WorldSnapshot` notification. A
  payload over `GameRpc.FragmentBudget` is split into ordered `SnapshotBatch`
  notifications. No snapshot is dropped. A send that the reliable path rejects
  is a peer failure: the server logs it and closes the peer with
  `RpcPeer.Close()`. The peer reconnects to a fresh join and full state.
- **Publish cadence.** The stage emits once per
  `NetworkingConfig.Protocol.SnapshotHz`, measured from tick deltas.
  `TickNumber`/`LastProcessedInput` semantics are unchanged. Send batching
  coalesces frames into one socket write per
  `Transport.SendBatchIntervalMs`.
- **Join-stream gating.** `BeginStream` opens when the join seats the session.
  The publish stage sends no per-tick deltas until `EndStream` after the level
  stream completes. The one-shot full-sync publish is exempt and stays the
  client's first snapshot.
- **World/voxel bodies.** Server-authoritative structures carrying a
  `NetworkComponent` flow through the same path. Their
  `TransformComponent`/`PhysicsComponent` are dirtied by the physics sync
  cycle, so the client snaps drift in its local prediction colliders.

See [voxel-edits](networking-voxel-edits.md) for authoritative voxel edits,
which flow as `VoxelEdit` notifications rather than snapshots.

## Client reconcile

`ClientReconcileSystem` (`WaywardBeyond.Client/Systems/ClientReconcileSystem.cs`):

- Drains `ClientInbound.Snapshots`, one ordered stream of full snapshots and
  fragment batches.
- Reassembles a batch with `FragmentAssembler`. A batch applies only when
  complete. A newer batch id or an out-of-order part drops the stale incomplete
  group.
- Applies complete snapshots in `TickNumber` order, so a full frame and a later
  batch never invert.
- On entering `Playing`, coalesces the queued burst: the newest frame applies
  fully, and every superseded frame still has its non-motion components applied
  first, so one-shot state seeded early in the join burst (the starter
  inventory) survives a motion-superseding burst.
- Applies `ServerOwned` components, frees despawned entities, trims
  `PendingInputComponent` by `LastProcessedInput`, and aligns the shared step
  with the snapshot tick.

## Client → server

`ClientReplicationSystem` (`WaywardBeyond.Client/Systems/ClientReplicationSystem.cs`)
iterates dirty `ClientOwned` components and sends one `WorldSnapshot`
notification per cadence.

- Discrete interaction taps are latched into the outbound edge queue.
  Inventory moves ride the same drain as `InventoryEvent` ops (see
  [inventory](networking-inventory.md)).
- Staged edges and ops clear only after the sending peer accepts the snapshot.
  A rejected send is a peer failure: the system requests the normal disconnect
  teardown and leaves the buffers staged.
- Upload is paced to `SnapshotHz`, not the ECS tick rate. Dirty components
  persist across skipped ticks.

## Dirty tracking

Store-mediated writes (`Alloc<T...>`, `AddOrUpdate`, `Entity.Add`) auto-mark a
component dirty. In-place mutation through a query `ref` is not auto-detected:
mutating systems use `store.QueryRef<T...>(...)` and the `Ref<T>.Write`
accessor. Replication caches its direction's `NetworkRegistry` component list
once per instance, so the hot path allocates nothing per entity. The client
collection is player-scoped.

## Tick tagging

`TickNumber` is the canonical sim tick (physics-step ordinal), not a replication
tick. Input is staged per sim tick and consumed exactly once per step. See
[prediction](networking-prediction.md).

## Source of truth

- `WaywardBeyond.Server/Systems/NetworkReplicationSystem.cs`
- `WaywardBeyond.Client/Systems/{ClientReplicationSystem,ClientReconcileSystem}.cs`
- `WaywardBeyond.Client/Networking/ClientInbound.cs`
- `WaywardBeyond.Networking/Rpc/{PayloadFragmenter,FragmentAssembler}.cs`
- `WaywardBeyond.Networking/Components/NetworkComponent.cs`

## Tests that pin this

- `Swordfish.Tests/RpcProtocolTests.cs` — fragment split, join, stale groups.
- `Swordfish.Tests/ServerWorldIntegrationTests.cs` — join, stream, save, leave,
  rejoin over loopback and TCP.
- `Swordfish.Tests` interaction-staging tests.
