# Bug: the server integration test writes the store off the server thread

- Type: bug
- Status: open
- Workflow: ../specs/issues.md

## Problem

`ServerWorldIntegrationTests.LoopbackJoinStreamSaveLeaveAndRejoin` and
`ServerWorldIntegrationTests.TcpJoinStreamSaveLeaveAndRejoin`
(`Swordfish.Tests/ServerWorldIntegrationTests.cs`) seed a voxel structure into a
live world from the test thread.

`SeedVoxelEntity` runs after `server.WorldHost.Run()` and calls
`world.Store.Alloc` and `world.Store.AddOrUpdate` directly. `ServerWorldHost`
ticks that world on the `"Server"` thread through a `ThreadWorker`
(`WaywardBeyond.Server/ServerWorldHost.cs`). The two threads therefore touch the
same `DataStore`, and the test thread builds the `VoxelObject` that the server
thread reads during the level stream.

`docs/development.md` forbids touching a world's store from a foreign thread. A
voxel container that records its constructing thread catches this immediately.
The access is currently harmless in practice because the race window is small,
and that is what makes it worth recording.

The tests need a way to run seeding on the server thread, or they need to seed
before `Run()` starts the worker.

## Acceptance criteria

- [ ] Both tests seed the store from the server thread, or seed a world that is
      not yet ticking.
- [ ] No test calls `DataStore.Alloc`, `DataStore.AddOrUpdate`, or
      `DataStore.Free` on a world owned by a running `ServerWorldHost`.
- [ ] A second structure is added to the same world while it runs, so the test
      covers the concurrent path rather than only the empty-world path.
