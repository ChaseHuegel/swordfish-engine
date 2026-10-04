# Improvement: Per-tick snapshot serialized once per client instead of once per tick

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

In `NetworkReplicationSystem.PublishStage` (`NetworkReplicationSystem.cs:121-149`)
the same `ComponentSnapshot[]` set is sent to every connected client, but
each client's `Send<WorldSnapshot>` serializes a **fresh full copy**: the
generated `WorldSnapshot.Serialize()` runs a `GetSize()` walk, allocates a
buffer, and `SerializeInto` walks every component per client, then the
transport frames it (`TcpTransport.Send`, `TcpTransport.cs:146-190`) per
client. With N clients the authoritative world is serialized N times per
tick - a 2x+ copy overhead per client at 60 Hz, plus per-client frame
allocations (compounded by the #14 pooling work).

## Acceptance criteria

- [ ] The authoritative `WorldSnapshot` (or its byte frame) is serialized
      once per tick and fanned out to each client; `LastProcessedInput`
      continues to be the per-client value (either a per-client header
      rewrite on a shared frame, or one cached serialization when the
      component set is shared).
- [ ] Measured: with N connected clients over `LocalConnection`,
      server-side serialization and allocations per tick do not grow
      linearly with N for the shared component set.
- [ ] Behavior unchanged: per-client snapshots still carry each client's
      own `LastProcessedInput` (pinned by
      `SessionRoutingTests.EachClientReceivesItsOwnProcessedInputAck`).