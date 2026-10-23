# Bug: the voxel container leaks live voxel arrays

- Type: bug
- Status: open
- Workflow: ../specs/issues.md

## Problem

`VoxelCodec.Encode` (`WaywardBeyond.Voxels/VoxelCodec.cs`) builds a new
`VoxelEntityData` that reuses `source.Chunks`. The encoded record aliases the
input arrays, so an edit to the input changes the encoded value after the fact.
The encoded value is then handed to a serializer or a store as if it owned its
content.

`VoxelObject.GetChunkInfos` had the same defect and no longer does. It returns a
deep copy and a test pins it (`SnapshotDoesNotAliasTheContainer`), so the save,
stream, and client serialize paths now receive arrays they own.

The remaining hazard is a caller that encodes a structure it still mutates, or a
second consumer added to a structure a codec result already holds. A save
boundary is not a hot path, so the copy costs nothing that matters.

## Acceptance criteria

- [x] `GetChunkInfos` returns chunks that do not alias the container. The doc
      comment states that the caller owns the result.
- [x] A test mutates the container after a snapshot and asserts the snapshot did
      not change.
- [ ] `VoxelCodec.Encode` does not alias `source.Chunks`. It copies, or its doc
      comment states that the caller keeps ownership of the input.
- [ ] Save, stream, and client serialize paths still produce identical bytes for
      an unmodified structure.
