# Bug: World-stream integrity and join — truncation risk, no timeout, infinite Loading

- Type: bug
- Status: open
- Workflow: ../specs/issues.md

## Problem

`ServerJoinSystem.StreamWorld` (`ServerJoinSystem.cs:223-239`) bursts the
entire world as one `WorldEntityAdd` per structure (un-acked) followed by
`WorldStreamComplete`, and `ClientJoinSystem` waits for that completion
indefinitely (`ClientJoinSystem.cs:115-122`). With #3's reliable queue,
stream frames are no longer evicted, but the remaining failure modes are:

- A world larger than what the client can drain in time stalls Loading
  with no progress signal and no bound. No join timeout exists anywhere.
- An undelivered `WorldStreamComplete` (or a mid-stream failure on a dying
  link) leaves the client in Loading forever with no watchdog. The
  character save is gated on `Playing`, so the session silently dies
  client-side.
- A client that cannot keep up makes the server's reliable queue for that
  peer grow without bound.

The documented ordering rule (delta `WorldSnapshot`s deferred until
`WorldStreamComplete`; the full-sync request rides the same join tick) is
enforced today only by the client's `Playing` gate, not stated on the wire
or kept explicit.

Decisions (locked): a client-side join-stream timeout with a new
`NetworkingSettings.JoinStreamTimeoutMs` key (default 60 s — generous for
slower connections); and the server disconnects a client whose reliable
send backlog stays over a configurable threshold, defaulting to 2x the #3
logging concern threshold.

## Acceptance criteria

- [ ] Client-side join-stream timeout: if `WorldStreamComplete` does not
      arrive within `JoinStreamTimeoutMs`, the client aborts the join,
      returns to the menu with the connection-lost notice, and tears the
      transport down.
- [ ] Server-side backlog policy: a client whose reliable send backlog
      stays over the configured disconnect threshold (default 2x the #3
      concern threshold) for a bounded period is disconnected, bounding
      per-client server memory. Config key documented in the
      `networking-transports.md` table.
- [ ] Stream integrity pinned by a test: a world of N structures delivers
      all N `WorldEntityAdd` frames plus `WorldStreamComplete` in order
      over a congested `TcpTransport` (using #3's queues).
- [ ] The ordering rule (no `WorldSnapshot` application before
      `WorldStreamComplete`; full-sync and stream on the same join tick)
      is documented in `networking-join.md` and kept true.