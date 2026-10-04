# Improvement: Bandwidth — snapshot-rate reduction and interval send batching

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

Playtest measured 350-380 KiB/s down / 120-150 KiB/s up while hosting and
playing with one remote client (remote saw 170 KiB/s down / 100 KiB/s up).
The per-tick path has no pacing: transforms and physics are re-dirtied
every physics step, so the wire carries full state per moving entity at
60 Hz, and the transport drains the send queue continuously
(`TcpTransport.SendLoop`, `TcpTransport.cs:279-309`) writing each frame as
its own `Write` with `NoDelay` - 60 tiny, uncoalesced segments per second
per connection, and the join/world-stream burst fires out at once.

Decision (locked): no traffic caps. Attack the inefficiency instead with
cadence and coalescing:

- **Snapshot rate.** `NetworkingSettings.SnapshotHz` (new key, default
  30): the server publish stage emits `WorldSnapshot` every
  `60 / SnapshotHz`-th sim tick. `TickNumber` and `LastProcessedInput`
  semantics are unchanged; the client reconcile already tolerates any
  cadence (`AlignTo` per snapshot with prediction between). Despawns and
  the full-sync publish ride the same cadence (at most one interval of
  delay).
- **Interval send batching.** `NetworkingSettings.SendIntervalMs` (new
  key, default 16): the TCP send thread drains both queues (reliable +
  per-tick) once per interval, coalescing all pending frames into a
  single socket write, then sleeps until the next interval. Clamped to
  <= `1000 / SnapshotHz` so no message waits longer than one snapshot
  interval. `LocalConnection` is untouched (no socket). This naturally
  paces the world-stream blast from #0004 and reduces syscalls and
  segment count.

Known trade (recorded): 30 Hz snapshot cadence means remote avatars
update 30 times per second with no client-side interpolation (the client
simulates only the local player). Expected to be acceptable for the
floaty motion model; verify visually and open a follow-up interpolation
issue if judged steppy.

## Acceptance criteria

- [ ] Baseline measured with the #0033 counters before and after the
      change; the two-player scenario shows roughly halved downstream
      bytes.
- [ ] `WorldSnapshot` publishes at `SnapshotHz` (default 30); the wire
      format and tick semantics are unchanged; despawns and full-sync
      publish within one interval.
- [ ] `TcpTransport` drains on `SendIntervalMs` (default 16, clamped to
      <= `1000 / SnapshotHz`), coalescing pending writes; reliable-queue
      frames are never dropped, only delayed by at most one interval.
- [ ] Visual check: remote player motion at 30 Hz with no interpolation
      is acceptable; a follow-up interpolation issue is opened if not.
- [ ] New config keys documented in `networking-transports.md` and
      `networking-replication.md` (docs pass).