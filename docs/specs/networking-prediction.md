# Networking — Client Prediction and Reconciliation

One subject: how the client predicts and corrects against the authoritative
server.

## The shared simulation step

Gameplay simulation is a shared, deterministic step that runs **per physics
step**, driven from `JoltPhysicsSystem.FixedUpdate` (the solver already
fixed-steps at 0.016s). It applies **tick-tagged commands**: each
`InputComponent` carries its target sim tick; multiple samples for the same
tick collapse to the newest. Both sides apply the identical command-per-step
mapping, which makes "same input sequence → same state" hold across two
independently-paced threads.

The canonical **sim tick = physics-step ordinal**. `TickNumber`,
`LastProcessedInput`, `ServerTickAtSample`, and `LastAckedInput`/
`LastAckedSnapshot` are rebased onto it. Snapshots publish `TickNumber` = sim
tick at publish.

The shared step is a per-world instance, not a DI singleton, because it holds
per-world sim state.

## Input pipeline

- `ClientInputSystem` samples `IInputService`, builds an `InputComponent` (with
  a monotonic `SequenceNumber` and `ServerTickAtSample` echoing the last applied
  snapshot tick), writes it to the local player, and stores a copy in
  `PendingInputComponent` (a 256-entry, sim-tick-keyed ring buffer).
- `ClientReplicationSystem` publishes dirty client-owned components upstream.
- On the server, input is staged per sim tick; the shared step consumes one
  staged command per sim tick, newest-for-tick on collision.

## Reconcile

`ClientReconcileSystem` (`Client.Core/Systems/ClientReconcileSystem.cs`)
receives server `WorldSnapshot`s, applies `ServerOwned` components (full state:
position, orientation, linear AND angular velocity), frees despawned entities,
trims `PendingInputComponent` by `LastProcessedInput`, and aligns the shared
step (`AlignTo` the snapshot's sim tick). `SnapshotAckTracker` records the
last applied snapshot tick.

Replay is per-sim-tick resolution, not a separate phase: the shared step's
command resolver reads the pending ring
(`PendingInputComponent.TryGetNewestAtOrBefore`) instead of the newest live
component, so every step after an `AlignTo` applies the surviving in-flight
inputs tick-exactly. The lookup pins the **newest-per-sim-tick collapse rule**:
it scans newest-to-oldest and returns the newest sample whose
`ServerTickAtSample` is at or below the sim tick — the exact scan the server's
`InputStageBuffer.TryGet` performs, so both sides consume the same
command-per-tick sequence and "same input sequence → same state" holds. The
ring is written by `ClientInputSystem` and read by no other caller; a player
without a ring (or a fully trimmed one) falls back to its live component.

## Prediction acks

- **Input ack** — `NetworkComponent.LastAckedInput` (advanced by the server,
  carried downstream in `WorldSnapshot.LastProcessedInput`) trims the client's
  prediction history.
- **Snapshot ack** — `SnapshotAckTracker`/`InputComponent.ServerTickAtSample`
  ties sampled input to the server tick it was sampled against.

These are gameplay-level bookkeeping for prediction. They are **not** a
transport reliability layer.

## Look and sensitivity

- Mouse sensitivity is a client-local setting, never networked. The client
  samples `IInputService.CursorDelta`, folds in sensitivity, and sends
  absolute look totals (`LookPitch`/`LookYaw`/`LookRoll`, radians).
- The shared step applies the per-sim-tick **difference** of those totals as
  body torque, decoupled from `dt`, so each step rotates by the full accumulated
  delta. Jolt integrates rotation.
- The local player's orientation stays client-predicted. Reconcile seats it
  once at spawn, then corrects only position/velocity, so the authoritative
  echo never snaps the view.

## Client vs server system order (client)

reconcile (apply incoming) → sample input (push pending) → replicate dirty
client-owned → predict via shared step → render reads final `Transform`.

## Source of truth

- `WaywardBeyond.Client/Systems/{ClientInputSystem,ClientReconcileSystem,ClientReplicationSystem}.cs`
- `WaywardBeyond.Gameplay/` — the shared simulation step
- `WaywardBeyond.Networking/Components/PendingInputComponent.cs`

## Tests that pin this

- `Swordfish.Tests` determinism / authority divergence tests (deliberately drift
  client state and watch reconcile correct it).
- Interaction replay and reconcile tests in `Swordfish.Tests`.