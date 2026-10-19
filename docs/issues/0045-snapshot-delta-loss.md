# Bug: Authoritative snapshot deltas are droppable

- Type: bug
- Status: open
- Workflow: ../specs/issues.md

## Problem

`TcpTransport.Send<T>` sends `WorldSnapshot` frames and the raw snapshot frames
from `SendRaw` to the bounded per-tick send queue. When that queue is full, the
oldest frame is dropped. `SendPriority` keeps `WorldSnapshot` out of the
reliable queue.

`WorldSnapshot` is a delta, not a full state. It carries three kinds of data
that a later snapshot does not repeat.

- `RemovedEntities` and `RemovedComponents`. A dropped removal leaves a ghost
  entity or a ghost component on the client. The only full sync happens at
  join.
- One-shot server-owned component updates. Inventory, equipment, game mode,
  and body view are sent only when dirtied. A dropped update stays stale until
  something dirties the component again.
- Client-authoritative commands. The upstream snapshot carries staged
  `InteractionEvent` edges and `InventoryEvent` ops. `ClientReplicationSystem`
  clears those buffers after `Send` returns success. The drop happens later on
  the send thread, so the client cannot know that a click or an inventory move
  was lost.

Transform and physics self-correct on the next tick. The other data does not.

Issue 0003 locked the assumption that "a drop of a per-tick snapshot is
harmless". That assumption is wrong for a delta snapshot on a reliable,
ordered transport. Loss tolerance needs full snapshots or acked baselines.
The protocol has neither.

Standard practice on TCP is to never drop an accepted frame. A peer that
cannot drain its backlog is disconnected and reconnects to a fresh full
state. The RPC migration provides that policy.

## Acceptance criteria

- [ ] No gameplay or authoritative state frame is dropped after the send path
      accepts it. Snapshots ride the reliable path.
- [ ] The drop-oldest per-tick send queue and the `SendPriority` allow-list
      are removed.
- [ ] A peer that cannot drain its backlog is closed under the configured
      backlog policy. It reconnects to a fresh join and a full sync.
- [ ] Client staged interaction and inventory buffers are cleared only after
      the containing snapshot is accepted by the reliable send path.
- [ ] A congestion test proves that no removal, one-shot component,
      interaction edge, or inventory op is lost.
- [ ] A stalled-peer test proves that the peer is disconnected instead of
      dropping frames.
- [ ] The docs pass updates `networking-transports`, `networking-replication`,
      and `networking-inventory`.

## Dependencies

This issue closes with the nsd-rpc server and client migrations in 0047 and
0048, and the legacy removal in 0050. The transport policy it depends on is
part of nsd-rpc 0.3.0. See
[the proposal](../proposals/nsd-rpc-0.3.0-transport.md).
