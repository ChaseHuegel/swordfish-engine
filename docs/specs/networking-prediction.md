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

- `ClientInputSystem` (`WaywardBeyond.Client/Systems/ClientInputSystem.cs:16`)
  samples `IInputService` every tick, builds an `InputComponent` (with a
  monotonic `SequenceNumber`, `ServerTickAtSample` echoing the last applied
  snapshot tick, and `DampenersDisabled`), writes it to the local player, and
  stages it in the player's `PendingInputComponent` ring (128 entries, newest at
  the end). It writes only when the movement, look totals, dampener state, or
  held buttons differ from the last sample it sent (`:196-208`), plus a 1 s
  keepalive. Both sides keep resolving the newest staged command, so an
  unchanged sample does not need a resend every tick.
- `ClientInputSystem` registers the rebindable
  `Movement/Toggle inertial dampeners` shortcut (`Key.Z` default,
  `WaywardBeyond.Client/Shortcuts/GameShortcuts.cs:19,83-94`) gated on
  `WaywardBeyond.IsPlaying() && !_interactionState.IsInteractionBlocked()`
  (`ClientInputSystem.cs:176-184`) and exposes `DampenersEnabled` (`:48-55`).
  `ResetSession` restores dampeners to enabled on join (`:157-162`).
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
error does not visibly jitter. An error past `CharacterDefaults.RECONCILE_SNAP_DISTANCE`
(2 m) is a discontinuity, not prediction error, so the position snaps. Orientation
targets the server orientation plus the look applied after the snapshot's ack, and
angular velocity is authority only while look is idle.

Remote player bodies run in the client's Jolt world too. The server replicates
their body configuration and collider, so the local prediction collides with
them the same way the authoritative world does. Their position correction uses
the same deadband, blend, and snap distance as the local player's position.
Orientation and scale are authoritative. Both reconcilers share the one distance
constant, so a respawn, teleport, or large server correction snaps for either
side instead of gliding across the world.

Remote mirrors have a `BodyViewComponent` but no `InputComponent`. The shared
step runs a second pass over `BodyViewComponent` bodies that skips any entity
with an `InputComponent`, so a remote mirror gets the identical damping as a
commandless server mirror without resolving client input or look history. The
server's own mirrors are input-driven, so its second pass is empty. The Jolt
solver applies world gravity to dynamic bodies in both worlds, so gravity stays
in parity without per-side code. A remote mirror's velocity is still
server-owned and refreshed by each snapshot; the between-snapshot damping keeps
the client's local integration on the same curve instead of coasting until the
next correction.

Replay is per-sim-tick resolution, not a separate phase: the shared step's
command resolver reads the pending ring (`StageRingExtensions.GetCommand`)
instead of the newest live component, so every step after an `AlignTo` applies
the surviving in-flight inputs tick-exactly. The lookup implements the
**newest-per-sim-tick rule**: it scans newest-to-oldest and returns the newest
sample whose `ServerTickAtSample` is at or below the sim tick, but only when
that sample was staged locally no more than `Protocol.CommandMaxAgeTicks`
(`network.toml`, default 120 sim ticks) ago. An older sample resolves no
command. Measuring age from the local staging sim tick (`StagedInput.StagedAtSimTick`)
rather than `ServerTickAtSample` keeps fresh input active on an idle world that
has not published a snapshot recently, and prevents a hostile client from
spoofing the age check with a forged `ServerTickAtSample`. The server's staged
ring uses the same lookup and the same configured bound, so both sides consume
the same command-per-tick sequence and "same input sequence → same state" holds.

The bound turns a stopped client or a network stall into idle input: the body
damps instead of re-applying the last held movement for as long as the client
stays silent. A hostile client cannot keep moving by withholding input. The
one-second client keepalive stays well inside the default bound, so a
well-behaved idle client keeps its held command active between keepalives.

The client ring is staged by `ClientInputSystem` and trimmed in place by
`ClientReconcileSystem` (`WaywardBeyond.Client/Systems/ClientReconcileSystem.cs:469-519`).
When trimming removes acknowledged entries, `ClientReconcileSystem` stores the
newest dropped sample in `PendingInputComponent.LastAcked`
(`WaywardBeyond.Client/Components/PendingInputComponent.cs:16`) and rebases
initial pre-snapshot clock timestamps on the first snapshot alignment.
`ClientPlayerMotionProcessor.ResolveCommand`
(`WaywardBeyond.Client/Systems/ClientPlayerMotionProcessor.cs:117-137`) falls
back to `PendingInputComponent.LastAcked` (subject to the same
`CommandMaxAgeTicks` bound) only when `Ring` holds no sample at or below the
current sim tick. There is no fallback to the live `InputComponent`, so a replay
gap or stalled stream resolves identically on both sides.

`SimulationStep.Execute` (`WaywardBeyond.Gameplay/SimulationStep.cs:158-224`)
always runs `DampAngular` (`:246-255`). It skips `DampLinear` (`:258-267`) only
when a resolved command sets `DampenersDisabled` to `true`, letting the body
coast while thrust and look continue to apply. A commandless step, a stale
command past `CommandMaxAgeTicks`, and `RemoteBodyDampAction` (`:142-156`) run
both `DampAngular` and `DampLinear`. A stalled input stream slows the body to
rest instead of letting it coast at its last velocity.

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
  totals, last input sample, sim tick, and snapshot ack, and both the client and
  the server forget the identity's last applied look history. A same-character
  rejoin reuses the identity, so the forget is required; the step drains forget
  requests before it applies commands.
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

- `WaywardBeyond.Client/Systems/{ClientInputSystem,ClientJoinSystem,ClientReconcileSystem,ClientReplicationSystem,ClientPlayerMotionProcessor}.cs`
- `WaywardBeyond.Client/Shortcuts/GameShortcuts.cs` — `TOGGLE_DAMPENERS` shortcut definition
- `WaywardBeyond.Client/Player/LocalPlayerReconciler.cs` — local-player orientation and angular reconciliation
- `WaywardBeyond.Gameplay/CharacterDefaults.cs` — `RECONCILE_SNAP_DISTANCE`, shared by both reconcilers
- `WaywardBeyond.Server/Systems/ServerJoinSystem.cs` — seats the mirror and forgets its prior look history
- `WaywardBeyond.Gameplay/SimulationStep.cs` — the shared simulation step
- `WaywardBeyond.Client/Components/PendingInputComponent.cs` — unacked ring plus `LastAcked` steady-command fallback
- `WaywardBeyond.Gameplay/Networking/StageRing.cs`
- `WaywardBeyond.Gameplay/Networking/StagedInput.cs` — input sample paired with its local staging sim tick
- `WaywardBeyond.Gameplay/Networking/StageRingExtensions.cs` — newest-per-tick command, `LastAcked` fallback, trim, and staleness bound
- `WaywardBeyond.Config/ProtocolConfig.cs` — `CommandMaxAgeTicks`
