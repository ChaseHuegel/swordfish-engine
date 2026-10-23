# Improvement: one chunk layout decides voxel indexing

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

Four places derive a chunk coordinate and a local voxel index from a world
coordinate, and they have already diverged.

- `VoxelObject.Set` and `VoxelObject.GetUnsafe`
  (`WaywardBeyond.Voxels/VoxelObject.cs`) normalise a negative coordinate with
  `x += size * Math.Abs(chunkX)` and then mask.
- `ChunkData.SampleEnumerator` (`WaywardBeyond.Voxels/ChunkData.cs`) decodes
  `x`, `y`, and `z` from a flat index with three shift expressions whose bounds
  differ from `VoxelObject`'s.
- `WaywardBeyond.Server/Generation/VoxelChunkWriter.cs` keeps its own
  `chunkSize`, `chunkMask`, `chunkShift`, `chunkShift2`, and `voxelsPerChunk`
  fields, its own negative-coordinate normalisation, and its own sorted chunk
  output.

`VoxelChunkWriter` is a duplicate of the container that `WaywardBeyond.Voxels`
now owns, minus the palette and minus the sampler. The server uses it to build
structure data at generation, so every voxel that reaches a level passes through
two implementations of the same index math.

The divergence is a real bug source. `VoxelObject.Sample` tests `nX > 0` for the
in-chunk fast path and `ChunkData` tests `nX < 0` for the out-of-chunk path, so
the two disagree about local coordinate 0.

## Acceptance criteria

- [ ] One type owns chunk size, mask, shifts, voxels-per-chunk, the local index,
      and the chunk offset. Every caller uses it.
- [ ] `VoxelChunkWriter` is deleted or becomes a thin wrapper over that type.
- [ ] The negative-coordinate normalisation exists in one place.
- [ ] A test covers local index 0 on each axis, chunk boundary coordinates, and
      negative coordinates for chunk sizes 8, 16, and 32.
