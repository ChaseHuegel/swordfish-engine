# Bug: Voxel palette count means two different things

- Type: bug
- Status: open
- Workflow: ../specs/issues.md

## Problem

`VoxelPalette.Count()` (`WaywardBeyond.Voxels/VoxelPalette.cs`) returns the
number of distinct ids in the table. That count includes id 0, the empty voxel.

`CollisionPostPass.ShouldProcessChunk`
(`WaywardBeyond.Client/Voxels/Processing/CollisionPostPass.cs`) reads it as a
voxel total:

- `totalVoxels = chunkData.Palette.Count()` is a distinct-id count.
- `emptyVoxels = chunkData.Palette.Count(id: 0)` is the empty-voxel count.
- The guard `if (emptyVoxels == totalVoxels)` is never true for a normal chunk.
  A fully empty 16³ chunk reports 1 and 4096.
- `nonEmptyVoxels = Math.Max(total, empty) - Math.Min(total, empty)` is an
  absolute difference. The same empty chunk yields 4095.

The guard never skips an empty chunk, and each empty chunk asks three lists for
4095 extra slots. The lists grow with every empty chunk in a structure.

The ambiguity is in the API. `Count` names both a distinct count and a total
without saying which it returns.

## Acceptance criteria

- [ ] `VoxelPalette` exposes one distinct-id count and one total-voxel count,
      with names that state which is which.
- [ ] `CollisionPostPass.ShouldProcessChunk` skips a chunk that has no
      non-empty voxel.
- [ ] The capacity hint equals the number of non-empty voxels in the chunk.
- [ ] A test covers an empty chunk, a single-voxel chunk, and a mixed chunk.
