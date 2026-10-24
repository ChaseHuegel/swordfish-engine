# Resource Lifetime: GPU Handles and Jolt Shapes

One subject: who disposes a native renderer or physics resource, and on which
thread. The voxel edit paths that create these resources are in
[networking-voxel-edits](networking-voxel-edits.md).

## GPU handles

`GLRenderContext` maps each managed handle to one GL handle in
`_linkedHandles` (`Swordfish/Graphics/SilkNET/OpenGL/GLRenderContext.cs:22`).
The map covers meshes, materials, shaders, textures, mesh renderers, and rect
renderers.

`IHandle.IsDisposed` states whether a handle is disposed
(`Swordfish.Library/Types/IHandle.cs:12`). A binder can reject a disposed
handle. `Handle`, `MeshRenderer`, and `RectRenderer` implement the property.

Disposal runs on the GL thread. The `Disposed` callback posts one work item to
the GL synchronization context. That work item removes the map entry and
disposes the GL handle (`GLRenderContext.cs:66-82`). The mapping must not change
on the ECS thread, because the render thread also reads the map. A disposed
A disposed mesh or renderer cannot create a new GL handle. The binder checks
`IsDisposed` before it creates anything (`GLRenderContext.cs:182-187`,
`:227-232`).
A mesh renderer owns one render target. The renderer's `Disposed` event removes
the target from `_renderTargets` (`GLRenderContext.cs:270-274`). The target
leaves the draw list in the tick that disposes the renderer.

## Voxel renderer cleanup

A client voxel rebuild builds new opaque and transparent renderers. It queues
the previous pair in `MeshRendererCleanup.MeshRenderers`
(`WaywardBeyond.Client/Voxels/Building/VoxelEntityBuilder.cs:101-102`).

`CleanupMeshRendererSystem` drains the queue every tick. It disposes each
renderer and its mesh
(`WaywardBeyond.Client/Systems/CleanupMeshRendererSystem.cs:8-20`). The query
does not depend on a live `MeshRendererComponent`, so a replaced renderer
cannot block its own cleanup.

`ClientCleanupSystem` drains the same queue before it frees a world entity
(`WaywardBeyond.Client/Systems/ClientCleanupSystem.cs:57-60`). The tick system
may already have run for the frame, so teardown cannot rely on the next tick.

`MeshRendererCleanup.DisposeMeshRenderers` owns the drain loop
(`WaywardBeyond.Client/Components/MeshRendererCleanup.cs:12-19`). Both paths
dispose queued renderers the same way.

## Jolt shape wrappers

`JoltPhysicsSystem.TryGetJoltShape` creates managed wrappers for native Jolt
shapes (`Swordfish/Physics/Jolt/JoltPhysicsSystem.cs:204-247`). The wrappers are
temporary. Jolt keeps its own reference after `SetShape` or `CreateBody`.

The caller disposes the shape wrapper after `SetShape` and after `CreateBody`
(`JoltPhysicsSystem.cs:335-340`, `:349-364`). A compound build disposes the
compound settings and each child wrapper before it returns the compound wrapper
(`JoltPhysicsSystem.cs:209-238`).

## Tests

`Swordfish.Tests/MeshRendererCleanupTests.cs` pins both queue drains:

- `CleanupTickDisposesQueuedRenderersAndMeshes`
- `ClientCleanupDrainsQueuedRenderersBeforeFreeing`

Each test asserts that the renderer, its mesh, and the queue are gone after the
tick.
