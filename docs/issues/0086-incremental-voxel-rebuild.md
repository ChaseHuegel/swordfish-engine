# Improvement: Reduce the cost of a voxel edit rebuild

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

A voxel edit rebuilds the whole structure, and the cost grows with the
structure size instead of the edit size.

`VoxelEntityBuilder.RebuildDirtyChunks` remeshes only dirty chunks
(`WaywardBeyond.Client/Voxels/Building/VoxelEntityBuilder.cs:153-220`), tracking
dirtied chunk offsets through `VoxelChunkRegistry`.

`VoxelChunkRegistry.ChunkColliders` caches collision data per chunk
(`WaywardBeyond.Client/Voxels/ChunkCollisionData.cs:10`).
`VoxelEntityBuilder.RebuildDirtyChunks` updates collision only for dirty chunks
and combines cached chunk shapes with `VoxelObjectBuilder.CombineCollisions`
(`WaywardBeyond.Client/Voxels/Building/VoxelObjectBuilder.cs:80-111`).

`CollisionPostPass` admits only surface voxels facing at least one empty
neighbor (`WaywardBeyond.Client/Voxels/Processing/CollisionPostPass.cs:38-44`),
reducing compound box shapes.

`JoltPhysicsSystem.TryGetJoltShape`
(`Swordfish/Physics/Jolt/JoltPhysicsSystem.cs:204-242`) deduplicates child shapes
by value equality, eliminating duplicate native `BoxShape` allocations.

Profiling with `dotnet-trace` showed that `BuildCollision` dropped from 247 ms
to less than 2 ms on large structures. Total rebuild time stays under 5 ms.

The renderer draws every structure render target. Nothing skips a structure
that is outside the view or fully occluded
(`Swordfish/Graphics/SilkNET/OpenGL/Renderers/GLInstancedRenderer.cs`).

Disposal is bounded since [0083](0083-voxel-edit-memory-leak.md), so the leak is
gone.

## Acceptance criteria

- [x] A rebuild remeshes only the chunks around the edit.
- [x] The collision shape contains only voxels on the surface of the structure.
- [ ] A structure that is outside the view does not build or submit a draw.
- [x] A benchmark records the per-edit cost for a large structure before and
      after the change.
