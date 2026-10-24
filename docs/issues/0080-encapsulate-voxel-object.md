# Improvement: encapsulate VoxelObject internals

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

`VoxelObject` (`WaywardBeyond.Voxels/VoxelObject.cs`) exposes five fields and
one method as `internal`, and carries a comment saying that is not good.

- `ChunkData` and `ChunkData.SampleEnumerator` read `_chunkShift`,
  `_chunkShift2`, `_chunkSize`, `_voxelsPerChunk`, and call `GetUnsafe`.
- `VoxelObject.Enumerator` takes the chunk dictionary and the lock as
  constructor arguments.

The comment is a `TODO`, which `docs/development.md` forbids in code.

The arrangement is also why the container cannot enforce its own invariants.
The sampling code re-derives the index math from raw fields, and the enumerator
can hold the container's lock without the container knowing. Every other
consumer in the repository works through the public API.

## Acceptance criteria

- [ ] `ChunkData` reaches the container through members that state a contract,
      not through raw fields.
- [ ] `VoxelObject.Enumerator` is constructed by the container, not supplied with
      its internals.
- [ ] `GetUnsafe` is gone or is a private detail of the container.
- [ ] The `TODO` comment is deleted because the problem it names is fixed.
