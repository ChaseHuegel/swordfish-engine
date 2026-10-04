# Improvement: Session heartbeats — server TPS and client sim-tick reporting, replacing the transport keepalive

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

Playtest: players want server performance visible - a live server TPS on
the F3 screen - and the session should carry enough signal to detect a
client whose simulation is falling behind the server. Today neither
exists: the only heartbeat on the wire is the TCP transport's
empty-type-tag keepalive frame (`TcpTransport._KEEPALIVE_FRAME`), which is
a socket-liveness byte, not an application message.

## Locked design

- **Server heartbeat.** New `ServerHeartbeatMessage { uint TPS; uint
  TickNumber; }` in `network.nsd`, emitted per connected client on the
  reliable queue at a slow cadence. `TPS` = server fixed-step count per
  wall second (averaged); `TickNumber` = current server sim tick.
- **Client heartbeat.** New `ClientHeartbeatMessage { uint TickNumber;
  uint LastAppliedSnapshotTick; }` at the same cadence. Sources are
  already wired: `ClientPlayerMotionProcessor.Step.CurrentSimTick` and
  `SnapshotAckTracker.LastAppliedSnapshotTick`. The client tick is
  `AlignTo`-ed from snapshots, so the delta vs server tick is a true lag
  reading, not a clock offset.
- **Heartbeat replaces the transport keepalive.** The app-level heartbeat
  becomes the new keepalive to avoid wasting traffic: remove
  `_KEEPALIVE_FRAME`, the `KeepaliveLoop`, the `KeepaliveIntervalMs`
  config key, and the empty-type-tag skip in `ReceiveLoop`. Both sides
  emit their heartbeat from connection establishment (not gated on join),
  and the interval is clamped below the connection timeout (the existing
  clamp pattern) so a live link always delivers a readable message within
  the timeout window.
- **Cadence.** New `NetworkingSettings.HeartbeatIntervalMs` (default
  1000), clamped below `ConnectionTimeoutMs`.
- **Server consumption.** Track each client's last reported tick; when
  `server tick - client tick` exceeds `NetworkingSettings.TickLagWarnThreshold`
  (default 10 sim ticks, ~0.17 s), log a warn with the per-client delta
  (feeds the #0023 logging work).
- **Client consumption.** The heartbeat feeds an F3 "server TPS" line
  (shared with the #0033 instrumentation screen); absence of heartbeats
  shows a clear "no server signal" state, never stale data.

## Acceptance criteria

- [ ] Both heartbeat messages added to `network.nsd`; server and client
      emit their heartbeat per connection per `HeartbeatIntervalMs` on
      the reliable queue, starting at connection establishment; interval
      clamped below `ConnectionTimeoutMs`.
- [ ] Transport keepalive removed: `_KEEPALIVE_FRAME`, `KeepaliveLoop`,
      the empty-type-tag skip in `ReceiveLoop`, and the
      `KeepaliveIntervalMs` key are gone; the idle-link test
      (`KeepaliveKeepsIdlePeerConnected`) is rewritten to drive app-level
      heartbeats and still passes.
- [ ] Server tracks per-client reported ticks and warns once per crossing
      when a client falls behind by more than `TickLagWarnThreshold`,
      with the delta in the log line.
- [ ] Client F3 shows the live server TPS from the heartbeat, with a
      no-signal state when heartbeats stop.
- [ ] Heartbeat bandwidth is negligible (verified via the #0033 counters).
- [ ] `networking-transports.md` documents the heartbeat-as-keepalive
      model and new config keys (`HeartbeatIntervalMs`,
      `TickLagWarnThreshold`); `networking-messages.md` documents both
      messages (docs pass).