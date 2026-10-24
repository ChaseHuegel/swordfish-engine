# Improvement: Networking docs drift — specs claim behavior the code does not have

- Type: improvement
- Status: done
- Workflow: ../specs/issues.md

## Problem

Several networking specs describe behavior that does not exist, or
overstate coverage, and would mislead the next reader:

- `networking-prediction.md` claims client replay with
  "newest-per-sim-tick collapse" on both sides - untrue today; becomes
  true only after #11 (implement replay).
- `networking-replication.md` describes `QueryDirty`/`QueryRemoved` as
  the replication iteration model; neither system uses them - code should
  converge via #12.
- `networking-transports.md` / `networking-overview.md` claim the
  `LocalConnection` "exercises the full protocol" - it exercises
  serialization only; framing, keepalive, and per-type demux are TCP-only
  and covered only by `TcpTransportTests`.
- `ServerConnectionHub.Receive`'s doc comment claims the connection list
  "is snapshotted so a client removed mid-poll is not enumerated" -
  `ConcurrentDictionary` enumeration is weakly consistent; the comment
  and any spec text must be corrected (also carried in #6).
- `networking-overview.md`'s "Current state" table cites the now-dead
  NATS-laden source list (see #16).

## Acceptance criteria

- [x] Each spec statement above is rewritten to match the code as it
      stands after #11, #12, and #16 land, with `file:line` pointers
      refreshed.
- [x] The "full protocol" claim for `LocalConnection` is scoped (wire
      serialization only; framing behavior is TCP-only).
- [x] No spec claims a behavior that is not pinned by a test.