# Bug: the two voxel sample producers disagree

- Type: bug
- Status: open
- Workflow: ../specs/issues.md

## Problem

`VoxelSample` has two producers and they do not mean the same thing.

- `ChunkData.SampleEnumerator` rotates the six neighbour offsets by the centre
  voxel's orientation before resolving them. It fills `ChunkOffset`,
  `ChunkCoords`, and `Coords`.
- `VoxelObject.Sample` uses fixed `±1` axis offsets, so it ignores the centre
  voxel's orientation. It leaves `ChunkOffset`, `ChunkCoords`, and `Coords` at
  `default`.

Consumers rely on different halves of that split. `MeshPostPass` and
`CubeMeshBuilder` read the rotated neighbours, so mesh culling follows a rotated
brick. `LightPropagationPass` and `AmbientLightPass` read the axis-aligned
neighbours, so light ignores a rotated brick.

The unset coordinate fields are a trap. Any consumer that reads `sample.Coords`
from `VoxelObject.Sample` gets `(0,0,0)` with no error.

## Acceptance criteria

- [ ] Both producers fill every coordinate field, or the type stops carrying
      coordinates that one producer cannot supply.
- [ ] Both producers agree on neighbour semantics for a rotated voxel, or the
      two forms have distinct types and doc comments that state the difference.
- [ ] Light propagation and ambient seeding behave correctly for a rotated
      brick.
- [ ] A test samples a rotated voxel through both producers and compares the
      six neighbours.
