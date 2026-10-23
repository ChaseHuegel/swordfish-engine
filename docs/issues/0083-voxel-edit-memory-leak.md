# Bug: Voxel edits leak mesh renderers and rebuild collision shapes

- Type: bug
- Status: open
- Workflow: ../specs/issues.md
- GitHub: [#612](https://github.com/ChaseHuegel/swordfish-engine/issues/612)

## Problem

Every voxel edit rebuilds the whole structure on each client. The rebuild
queues the previous opaque and transparent `MeshRenderer` instances into
`MeshRendererCleanup.MeshRenderers`
(`WaywardBeyond.Client/Voxels/Building/VoxelEntityBuilder.cs:97-102`).

`CleanupMeshRendererSystem` never drains that bag. It returns while
`MeshRendererComponent.Bound` is false
(`WaywardBeyond.Client/Systems/CleanupMeshRendererSystem.cs:13`). Nothing sets
`Bound` to true. Commit `92d8e5ca` removed `MeshRendererSystem`, which was the
only writer (`Swordfish/Graphics/MeshRendererSystem.cs` is empty), and
`Swordfish/EngineContainer.cs` no longer registers it. The field remains at
`Swordfish/Graphics/MeshRendererComponent.cs:11`.

The leak is unbounded:

- Two renderers and two meshes are retained per edit.
- `GLRenderContext._linkedHandles` retains the matching mesh and renderer.
- `GLRenderContext._renderTargets` retains the matching `GLRenderTarget`.
- The old GPU vertex and element buffers are never deleted because
  `Mesh.Dispose` never runs.
- `GLInstancedRenderer` iterates and draws every stale render target every
  frame, including empty ones. Frame cost grows with the edit count.

A restart clears the process-wide `GLRenderContext` and restores memory and
framerate. A world teardown does not. `ClientCleanupSystem` disposes only the
current renderer (`WaywardBeyond.Client/Systems/ClientCleanupSystem.cs:57-61`)
and never drains the cleanup bag.

Renderer disposal also races the render thread. `GLRenderContext.OnHandleDisposed`
removes `_linkedHandles` on the ECS thread and posts only the GL disposal
(`Swordfish/Graphics/SilkNET/OpenGL/GLRenderContext.cs:66-72`). A stale render
frame can rebind the disposed renderer and recreate a VAO that never gets
disposed. Cleanup must remove the mapping on the GL thread, or reject disposed
handles.

The per-edit full rebuild is the amplifier. `VoxelObjectBuilder.Build` rebuilds
the full mesh and the full `CompoundShape` on every dirty tick
(`WaywardBeyond.Client/Voxels/Building/VoxelObjectBuilder.cs:15-31`). The edit
position does not matter, so edits inside one chunk still trigger the leak.

The collision path has a second memory cost. The client and server create one
box per solid voxel
(`WaywardBeyond.Client/Voxels/Processing/CollisionPostPass.cs:31-43`,
`WaywardBeyond.Server/Systems/VoxelColliderBuilder.cs:57-73`). `HasAny()` includes
the center voxel, so the guard admits every solid voxel, including interior
ones. Each edit then converts the whole compound to Jolt with one `BoxShape`
wrapper per child (`Swordfish/Physics/Jolt/JoltPhysicsSystem.cs:204-232`,
`:271-346`). The wrappers and the compound are never disposed. Their release
depends on finalizers, so a large structure creates a large native and
finalization backlog per edit.

A harness confirmed the renderer gate. `Bound=false` leaves the renderer and
mesh undisposed with one item in the bag. `Bound=true` disposes both and empties
the bag.

## Acceptance criteria

- [ ] `CleanupMeshRendererSystem` disposes every queued renderer and mesh. The
      `Bound` gate no longer blocks cleanup.
- [ ] A rebuild leaves `GLRenderContext._renderTargets` and `_linkedHandles`
      bounded. They do not grow with the edit count.
- [ ] Renderer disposal does not race a render frame and cannot resurrect a
      disposed renderer or mesh.
- [ ] A regression test queues a renderer and mesh, ticks the cleanup system
      with `Bound=false`, and asserts both are disposed and the bag is empty.
- [ ] `ClientCleanupSystem` drains any queued renderers before it frees a world
      entity.
- [ ] The Jolt collider rebuild disposes the compound and child shape wrappers
      after `SetShape` and `CreateBody`.
- [ ] A long edit session plateaus in memory and framerate instead of climbing.
- [ ] A separate follow-up covers incremental remesh, surface-only collision,
      and visibility gating. Those are not required to stop the leak.
