# Improvement: F3 network statistics — packets/bytes counters and player count

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

Playtest: the debug screen should show packets and bytes in/out (totals
and per-second) plus player count. No counters exist on any transport
today, so the #0034 bandwidth work is not measurable; the F3 screen has no
network section.

## Locked design

Instrumentation is layered: counters (measurement) -> Samplers (rate
smoothing) -> overlay (display), using the engine `Sampler` type already
established by `PerformanceStatsOverlay`.

- **Counters.** `TcpTransport` and `LocalConnection` expose four monotonic
  counters each: `PacketsSent`/`PacketsReceived`, `BytesSent`/
  `BytesReceived`, with `Interlocked` increments on the actual send and
  receive paths. No sampling logic inside the transports.
- **Samplers.** A new `NetworkStatsOverlay : IDebugOverlay` owns four
  `Sampler`s (length 120, matching `PerformanceStatsOverlay`) and records
  per-second rates each render frame from counter deltas divided by the
  frame delta (the same math the existing overlay uses for FPS,
  `PerformanceStatsOverlay.cs:24-25`).
- **Display.** The overlay renders its own F3 section in the established
  `M:/A:/L:/H:` idiom: packets in/out per second, bytes in/out per
  second, plain totals, and player count plus server TPS read from the
  #0032 heartbeat consumer (shared client singleton - coupling with
  #0032). A clear no-signal state when disconnected.
- **Player count** comes over the wire, not by guessing:
  `ServerHeartbeatMessage` from #0032 gains `uint PlayerCount`
  (server-stamped from `ServerConnectionHub.Count`). Land this alongside
  or after #0032.
- **Scope.** Per-client breakdown server-side (a host seeing each peer's
  counters) is out of scope for this pass; the hub may expose an
  aggregate later if the dedicated-server work needs it.

## Acceptance criteria

- [ ] Send/receive counters on both transports are incremented on every
      frame and exposed; the #0014 pooling work does not change counter
      semantics (counts events and bytes, not retained buffers).
- [ ] `NetworkStatsOverlay` records and renders per-second rates (packets
      and bytes in/out), totals, player count, and the #0032 server TPS,
      with a no-signal state when disconnected.
- [ ] `ServerHeartbeatMessage.PlayerCount` added per the #0032 coupling
      and documented in `networking-messages.md`.
- [ ] Tests: known packet/byte sequences over `LocalConnection` and a
      `TcpTransport` pair produce exact counter values; counter
      thread-safety holds under the transport's send threads.