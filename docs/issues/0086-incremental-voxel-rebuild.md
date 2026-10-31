# Improvement: Reduce the cost of a voxel edit rebuild

- Type: improvement
- Status: done
- Workflow: ../specs/issues.md

## Problem

A voxel edit rebuilt the whole structure, and the cost grew with the structure
size instead of the edit size.

`VoxelEntityBuilder.RebuildDirtyChunks` remeshes only dirty chunks
(`WaywardBeyond.Client/Voxels/Building/VoxelEntityBuilder.cs:146-243`), tracking
dirtied chunk offsets through `VoxelChunkRegistry`.

`VoxelChunkRegistry.ChunkColliders` caches collision data per chunk
(`WaywardBeyond.Client/Voxels/ChunkCollisionData.cs:10`).
`VoxelEntityBuilder.RebuildDirtyChunks` updates collision only for dirty chunks
and combines cached chunk shapes with `VoxelObjectBuilder.CombineCollisions`
(`WaywardBeyond.Client/Voxels/Building/VoxelObjectBuilder.cs:109-144`).

`CollisionPostPass` admits only surface voxels facing at least one empty
neighbor (`WaywardBeyond.Client/Voxels/Processing/CollisionPostPass.cs:38-44`),
reducing compound box shapes.

`JoltPhysicsSystem.TryGetJoltShape`
(`Swordfish/Physics/Jolt/JoltPhysicsSystem.cs:206-241`) deduplicates child shapes
by value equality, eliminating duplicate native `BoxShape` allocations.

Profiling with `dotnet-trace` showed that `BuildCollision` dropped from 247 ms
to less than 2 ms on large structures.

Camera culling is a view concern. `GLRenderContext.Tick` submits every live
mesh renderer (`Swordfish/Graphics/SilkNET/OpenGL/GLRenderContext.cs:111-119`).
The render stages test each target against the camera frustum and against each
shadow cascade or cube-face frustum (`GLInstancedRenderer.cs`). A structure
outside the view is not drawn.

Disposal is bounded since [0083](0083-voxel-edit-memory-leak.md), so the leak is
gone.

## Benchmark

`WaywardBeyond.Client.Benchmarks` measures the rebuild path. Run it with:

```
dotnet run -c Release --project WaywardBeyond.Client.Benchmarks -- --filter "*BuildChunk*" --job short
```

Short run on AMD Ryzen 5 3600X, .NET 10.0.14:

| Benchmark | Mean | Allocated |
|---|---|---|
| `BuildChunk_Dense` | 540.65 us | 348.46 KB |
| `BuildChunk_Sparse` | 97.36 us | 130.72 KB |
| `BuildChunkCollision_Dense` | 210.57 us | 68.73 KB |
| `BuildChunkCollision_Sparse` | 19.90 us | 13.07 KB |
| `BuildLighting_SparseChunk` | 427.5 us | 1.02 MB |

The dense chunk is mostly culled, so its mesh build is mostly sampling cost. A
border edit touches up to seven chunks. At the sparse cost that is under 1 ms.
At the dense cost it is about 5.3 ms. The per-tick budget caps the work at 10 ms.

## Acceptance criteria

- [x] A rebuild remeshes only the chunks around the edit.
- [x] The collision shape contains only voxels on the surface of the structure.
- [x] A structure that is outside the view does not submit a draw.
- [x] A benchmark records the per-edit cost for a large structure.
