# Bug: Loading-state snapshot pile-up — memory spike and join-time hitch

- Type: bug
- Status: done
- Workflow: ../specs/issues.md

## Problem

A joined-but-still-streaming client keeps receiving per-tick
`WorldSnapshot`s from the publish stage while `ClientReconcileSystem` is
gated off (`WaywardBeyond.GameState < GameState.Playing`,
`ClientReconcileSystem.cs:52-55`). A slow join (large world, view-entity
builds) lasts seconds; every tick's snapshot then accumulates in the
client's unbounded per-type receive queue (memory spike), and on `Playing`
it all drains in one tick - full component application, entity
materialization, pending-input trim, and `AlignTo` per snapshot - a
multi-frame hitch at the worst possible moment.

With #3 and #4 in place, the clean fix is server-side: once a client's
world stream is enqueued (the reliable queue guarantees delivery), the
server stops publishing deltas to that client until it observes the stream
is complete.

## Acceptance criteria

- [x] The server publishes no per-tick `WorldSnapshot`s to a client while
      its join stream is in flight (from join until `WorldStreamComplete`
      is enqueued); the full-sync publish remains the client's first
      snapshot.
- [x] Defensive client-side: entering `Playing` applies only the newest
      queued snapshot (older queued snapshots are coalesced or dropped),
      so a burst left over from a previous join can never hitch the first
      play frame.
- [x] Test: a client stuck in Loading for N ticks accumulates no more than
      one server-queued snapshot; entering `Playing` performs exactly one
      `ApplySnapshot`.
- [x] `networking-replication.md` documents the per-client publish gating
      during join.