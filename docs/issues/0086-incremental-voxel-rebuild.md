# Improvement: Reduce the cost of a voxel edit rebuild

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

A voxel edit rebuilds the whole structure, and the cost grows with the
structure size instead of the edit size.

`VoxelObjectBuilder.Build` rebuilds the full mesh and the full `CompoundShape`
on every dirty tick (`WaywardBeyond.Client/Voxels/Building/VoxelObjectBuilder.cs:15-31`).
The edit position does not matter. An edit inside one chunk rebuilds every
chunk.

The collision pass creates one box per solid voxel
(`WaywardBeyond.Client/Voxels/Processing/CollisionPostPass.cs:31-43`). The
`HasAny()` guard includes the center voxel, so it admits interior voxels as
well as surface voxels. The server collider has the same cost
(`WaywardBeyond.Server/Systems/VoxelColliderBuilder.cs:57-73`). Jolt then
converts the whole compound on each rebuild
(`Swordfish/Physics/Jolt/JoltPhysicsSystem.cs:204-247`).

The renderer draws every structure render target. Nothing skips a structure
that is outside the view or fully occluded
(`Swordfish/Graphics/SilkNET/OpenGL/Renderers/GLInstancedRenderer.cs`).

Disposal is bounded since [0083](0083-voxel-edit-memory-leak.md), so the leak is
gone. This issue covers the remaining per-edit cost.

## Acceptance criteria

- [ ] A rebuild remeshes only the chunks around the edit.
- [ ] The collision shape contains only voxels on the surface of the structure.
- [ ] A structure that is outside the view does not build or submit a draw.
- [ ] A benchmark records the per-edit cost for a large structure before and
      after the change.
