# Bug: the voxel container leaks live voxel arrays

- Type: bug
- Status: open
- Workflow: ../specs/issues.md

## Problem

`VoxelObject.GetChunkInfos()` (`WaywardBeyond.Voxels/VoxelObject.cs`) returns a
`ChunkInfo[]` whose `Chunk.Voxels` arrays are the live arrays inside the
container. It copies the segment, not the data.

Callers keep those arrays past the call:

- `LevelSaveService.CaptureStructureAction` writes them to SQLite.
- `VoxelWorldEntityFactory.ToVoxelEntityData` hands them to the level stream.
- `VoxelEntityModelSerializer.Serialize` serializes them on the client.

The container stays reachable and mutable through the returned value. A caller
can edit a structure through a save snapshot. A caller can also hold the arrays
after the structure changed, and never knows which frame it captured.

`VoxelCodec.Encode` repeats the pattern. It builds a new `VoxelEntityData` that
reuses `source.Chunks`, so the encoded record aliases the input. The encoded
value is then handed to a serializer or a store as if it owned its content.

The container must hand out a value callers own. A save or stream boundary is
not a hot path, so the copy costs nothing that matters.

## Acceptance criteria

- [ ] `GetChunkInfos` returns chunks that do not alias the container. The
      doc comment states that the caller owns the result.
- [ ] `VoxelCodec.Encode` does not alias `source.Chunks`. It copies, or the
      caller supplies arrays it already owns.
- [ ] A test mutates the container after a snapshot and asserts the snapshot
      did not change.
- [ ] Save, stream, and client serialize paths still produce identical bytes
      for an unmodified structure.
