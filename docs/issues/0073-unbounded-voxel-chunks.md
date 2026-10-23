# Bug: voxel chunks are unbounded and chunk coordinates truncate

- Type: bug
- Status: open
- Workflow: ../specs/issues.md

## Problem

`VoxelObject.Set` and `VoxelObject.GetUnsafe`
(`WaywardBeyond.Voxels/VoxelObject.cs`) convert a world coordinate to a chunk
coordinate with `(short)(x >> chunkShift)`. A world coordinate outside the
`short` chunk range truncates and wraps.

`Set(1_000_000, 0, 0, voxel)` resolves to chunk `-2592` and writes there.
`int.MinValue` resolves to chunk 0. Neither call reports an error, and both
corrupt a resident chunk.

The container also never shrinks. `_chunks` gains an entry for every distinct
chunk a caller touches and there is no `Remove`, `Clear`, or prune. A structure
that a client edits at scattered coordinates grows without limit for the
lifetime of the world.

The server writes a cell the client chose. `InteractionResolver` checks reach
against the hinted cell, and a lying client supplies both the cell and the
origin, so the reach check passes for any coordinate inside the structure. The
container is the last place that can bound the write.

## Acceptance criteria

- [ ] `Set`, `Get`, and `Sample` reject a coordinate whose chunk offset does not
      fit `ChunkInfo.OffsetX/Y/Z`. The rejection is explicit and does not write.
- [ ] The container documents a bound on resident chunks, or a caller can
      release chunks.
- [ ] A test sets a coordinate past the chunk range and asserts the container is
      unchanged.
- [ ] The server interaction path reports the rejection as a log line and does
      not apply the edit.
