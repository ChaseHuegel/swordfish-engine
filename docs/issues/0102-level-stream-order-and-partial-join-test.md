# Improvement: Prove level-stream order and that a partial stream cannot enter play

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

0051 required a join test. The test must prove two things. Every level entity
and `LevelStreamComplete` arrive in order. A partial stream never enters
`Playing`. The test was not written. 0051 closes with that criterion moved here.

The current join tests (`LoopbackJoinStreamSaveLeaveAndRejoin` and
`TcpJoinStreamSaveLeaveAndRejoin` in `Swordfish.Tests/ServerWorldIntegrationTests.cs`)
only assert that `sink.Chunks` is not empty and that a seeded voxel entity
round-trips. They do not prove:

- each entity's chunks are contiguous and ordered, with `PartIndex` running
  `0..PartCount-1`;
- entities arrive in the server's `stream.Entities` order;
- `LevelStreamComplete` resolves only after the last chunk;
- a stream that ends with an active `FragmentAssembler` leaves the client in
  `Loading` and never enters `Playing`.

`ClientJoinSystem.PollStream` already checks `_levelAssembler.IsActive` after
the response and waits for `_pendingBuilds`. The behavior exists. The tests do
not pin it.

The join system summary still references the removed `LevelEntityAdd` type
(`WaywardBeyond.Server/Systems/ServerJoinSystem.cs:25`). The docs pass for this
issue must correct it to `LevelStreamChunk`.

## Acceptance criteria

- [ ] A join test streams at least two structures. It asserts that each entity's
      parts are contiguous and ordered, that entities follow server order, and
      that the response completes after the final part.
- [ ] A test ends a stream with an incomplete fragment. It asserts that the
      client stays in `Loading`, that the join fails, and that
      `GameState.Playing` is never set.
- [ ] A failed or aborted stream gets the same no-`Playing` assertion.
- [ ] The tests seed entities before `WorldHost.Run()` or on the server thread,
      so they do not add another off-thread store write (0082).
- [ ] `ServerJoinSystem`'s summary names `LevelStreamChunk`, not the removed
      `LevelEntityAdd`.
