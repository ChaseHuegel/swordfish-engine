# Networking — Client Prediction and Reconciliation

One subject: how the client predicts and corrects against the authoritative
server.

## The shared simulation step

Gameplay simulation is a shared, deterministic step that runs **per physics
step**, driven from `JoltPhysicsSystem.FixedUpdate` (the solver fixed-steps at
`IPhysics.FixedTimestamp`, tunable via `physics.toml`). It applies
**tick-tagged commands**: each
`InputComponent` carries its target sim tick; multiple samples for the same
tick collapse to the newest. Both sides apply the identical command-per-step
mapping, which makes "same input sequence → same state" hold across two
independently-paced threads.

The canonical **sim tick = physics-step ordinal**. `TickNumber`,
`LastProcessedInput`, `ServerTickAtSample`, and `LastAckedInput` are rebased onto
it. Snapshots publish `TickNumber` = sim tick at publish.

The shared step is a per-world instance, not a DI singleton, because it holds
per-world sim state.

## Input pipeline

- `ClientInputSystem` samples `IInputService` every tick, builds an
  `InputComponent` (with a monotonic `SequenceNumber` and `ServerTickAtSample`
  echoing the last applied snapshot tick), writes it to the local player, and
  stages it in the player's `PendingInputComponent` ring (128 entries, newest at
  the end). It writes only when the movement, look totals, or held buttons differ
  from the last sample it sent, plus a 1 s keepalive. Both sides keep resolving
  the newest staged command, so an unchanged sample does not need a resend every
  tick.
- `ClientReplicationSystem` publishes dirty client-owned components upstream.
- On the server, input is staged per sim tick; the shared step resolves the
  newest staged command at or before the current sim tick.

## Reconcile

`ClientReconcileSystem` (`WaywardBeyond.Client/Systems/ClientReconcileSystem.cs`)
drains the ordered `ClientInbound.Snapshots` stream.
`FragmentAssembler` joins a batch only when it is complete, and a newer batch
drops a stale incomplete group. The system applies `ServerOwned` components
(full state: position, orientation, linear and angular velocity), frees
despawned entities, trims `PendingInputComponent` by `LastProcessedInput`, and
aligns the shared step (`AlignTo` the snapshot's sim tick).
`SnapshotAckTracker` records the last applied snapshot tick.

For the local player, position and linear velocity are authority. Small position
errors are ignored and larger ones are blended over a few snapshots, so prediction
error does not visibly jitter. Orientation targets the server orientation plus the
look applied after the snapshot's ack, and angular velocity is authority only while
look is idle.

Replay is per-sim-tick resolution, not a separate phase: the shared step's
command resolver reads the pending ring (`StageRingExtensions.GetCommand`)
instead of the newest live component, so every step after an `AlignTo` applies
the surviving in-flight inputs tick-exactly. The lookup implements the
**newest-per-sim-tick rule**: it scans newest-to-oldest and returns the newest
sample whose `ServerTickAtSample` is at or below the sim tick. The server's
staged ring uses the same lookup, so both sides consume the same command-per-tick
sequence and "same input sequence → same state" holds.

The client ring is staged by `ClientInputSystem` and trimmed in place by
`ClientReconcileSystem`. A player without a ring, or a ring with no sample at or
below the sim tick, resolves no command. That is idle input and it resolves the
same way on both sides: the step damps the body and applies no movement. There is
no fallback to the live component, so a replay gap cannot move one side and not
the other.

Damping runs every step, with or without a command. A stalled input stream slows
the body to rest instead of letting it coast at its last velocity.

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
- The step keys the last applied totals by entity identity (`Uuid`), not by the
  store index. A freed index that is recycled with a new identity starts with no
  delta, so it cannot inherit the previous session's totals.
- A join resets session-scoped prediction state. The client clears its look
  totals, last input sample, and snapshot ack, and both the client and the server
  forget the identity's last applied look history. A same-character rejoin reuses
  the identity, so the forget is required; the step drains forget requests before
  it applies commands.
- The local player's look stays client-predicted. `JoinAccept` carries the
  server-resolved spawn transform, and `ClientJoinSystem` seeds the local
  `TransformComponent` with it when it seats the player.
- Collision rotation is authoritative. `ClientReconcileSystem` delegates local
  player motion to `LocalPlayerReconciler`, which targets
  `serverOrientation * pendingLook`, where `pendingLook` is the look applied after
  the snapshot's ack, and slerps the local orientation toward it over about
  100 ms. The current look is preserved, so the view never rewinds to the
  server's older look. When the look is idle, the server's angular velocity is
  applied and the target advances with it.

## Client vs server system order (client)

shared step (inside `JoltPhysicsSystem.Tick`) → `ClientPlayerMotionProcessor` →
sample input (push pending) → replicate dirty client-owned → reconcile (apply
incoming, correct local-player orientation) → render reads final `Transform`.

## Source of truth

- `WaywardBeyond.Client/Systems/{ClientInputSystem,ClientJoinSystem,ClientReconcileSystem,ClientReplicationSystem}.cs`
- `WaywardBeyond.Client/Player/LocalPlayerReconciler.cs` — local-player orientation and angular reconciliation
- `WaywardBeyond.Server/Systems/ServerJoinSystem.cs` — seats the mirror and forgets its prior look history
- `WaywardBeyond.Gameplay/SimulationStep.cs` — the shared simulation step
- `WaywardBeyond.Client/Components/PendingInputComponent.cs`
- `WaywardBeyond.Gameplay/Networking/StageRing.cs`
