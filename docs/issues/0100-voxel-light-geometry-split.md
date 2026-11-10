# Improvement: Separate voxel light from geometry

- Type: improvement
- Status: open
- Workflow: [../specs/issues.md](../specs/issues.md)
- Plan: [../plans/voxel-remesh-rendering.md](../plans/voxel-remesh-rendering.md) (Phase G2)

## Problem

Voxel light is baked into the mesh vertex color. `MeshPostPass` reads the
neighbour light level and writes it into the vertex color
(`WaywardBeyond.Client/Voxels/Processing/CubeMeshBuilder.cs:253-258`). A light
change therefore forces a mesh rebuild.

Light propagation marks every chunk that receives light dirty
(`WaywardBeyond.Client/Voxels/Processing/VoxelLightingService.cs:321-323`).
`RebuildDirtyChunks` then remeshes the chunk and rebuilds its collision
(`WaywardBeyond.Client/Voxels/Building/VoxelEntityBuilder.cs:216-230`). A lamp
placement in a 48x48 floor test dirtied 10 chunks. That is 10 full chunk
remeshes, 10 collision rebuilds, 10 `ReconcileVoxelEntities` store scans, and a
new compound collider.

The temporary fix in Phase G2 adds a light-only dirty kind and skips collision.
The mesh is still rebuilt. That is not enough.

## Acceptance criteria

- [ ] A light-only edit does not rebuild chunk collision.
- [ ] A light-only edit does not rebuild chunk vertex positions, normals, or
      indices.
- [ ] Light is stored apart from the geometry. A light change updates only the
      light data.
- [ ] A lamp placement in a large structure costs a bounded light update and no
      chunk remesh.
- [ ] The benchmark records the light-edit cost before and after the split.
- [ ] The docs describe the light storage contract and the update path.
