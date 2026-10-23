# Bug: Bound how old a resolved input command may be

- Type: bug
- Status: open
- Workflow: ../specs/issues.md

## Problem

`StageRingExtensions.GetCommand` returns the newest staged input sample whose
`ServerTickAtSample` is at or below the current sim tick, with no age limit. A
client that sends one movement sample and then stops keeps that sample selected,
so the server re-applies the movement every sim tick indefinitely. The staged ring
only drops the oldest entry at capacity, and the stale entry stays the newest.

A network stall has the same shape for its duration: the player keeps moving on
the last held input instead of slowing down. A hostile client can exploit it to
move without sending input. The one-second keepalive only covers well-behaved
clients.

The fix needs a tunable maximum age, which belongs in config per issue 0054.

## Acceptance criteria

- [ ] A resolved sample older than the configured maximum age is treated as idle
      input.
- [ ] The bound is a config key with a default and a schema-table row.
- [ ] A test stages one sample, advances the sim tick past the bound, and asserts
      the body damps instead of moving.
- [ ] Client and server apply the same bound.
