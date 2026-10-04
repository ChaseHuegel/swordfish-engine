# Bug: Client prediction does not perform the documented replay; `PendingInputComponent` is dead weight

- Type: bug
- Status: open
- Workflow: ../specs/issues.md

## Problem

`networking-prediction.md` describes a replay model: reconcile trims
`PendingInputComponent` by the server ack and "replays the surviving
inputs through the shared step," with the "newest-per-sim-tick collapse"
rule on both sides. The code does none of that:

- `ClientPlayerMotionProcessor.ResolveCommand`
  (`ClientPlayerMotionProcessor.cs:107-112`) ignores the `simTick`
  argument and applies the newest live `InputComponent` on the entity.
- `PendingInputComponent.GetPending` (`PendingInputComponent.cs:56`) has
  zero call sites in the repo - the 256-entry ring is written
  (`ClientInputSystem.cs:101`) and trimmed (`ClientReconcileSystem.cs:172-187`)
  but never read.

Consequences: after a reconcile `AlignTo`, the client does not re-apply
in-flight inputs the server has not yet processed, so prediction accuracy
between snapshots is bounded by live application; and the same-tick
collapse the docs claim (two samples sharing a `ServerTickAtSample`
collapse to the newest on both sides) happens only server-side. Docs and
code disagree, and the staged buffer is pure overhead today.

Decision (locked): implement the documented replay (option a). The wire
format, tick tagging, and server staging already exist and are keyed for
replay. Reconcile should step the surviving pending inputs forward from
the acked tick after each authoritative apply, applying the client-side
newest-per-sim-tick collapse so "same input sequence -> same state" holds
much more precisely.

## Acceptance criteria

- [ ] After a reconcile `AlignTo`, the client re-applies the surviving
      pending inputs through the shared step, tick-exactly, with
      newest-per-sim-tick collapse matching the server's staging.
- [ ] `networking-prediction.md` matches the code exactly (replay path and
      its collapse rule described as implemented).
- [ ] The replay path (not just the ring buffer write/trim) is exercised
      by tests: a determinism test proves the client step converges to the
      server state from the same pending input sequence; the existing
      authority-divergence tests still pass.
- [ ] No dead replicated code remains: `GetPending` (or the replay
      equivalent) has a caller.