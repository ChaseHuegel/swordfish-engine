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
disposes the GL handle (`GLRenderContext.cs:81-97`). The mapping must not change
on the ECS thread, because the render thread also reads the map. A disposed
mesh or renderer cannot create a new GL handle. The binder checks
`IsDisposed` before it creates anything (`GLRenderContext.cs:201-206`,
`:246-251`).
A mesh renderer owns one render target. The renderer's `Disposed` event removes
the target from `_renderTargets` (`GLRenderContext.cs:288-292`). The target
leaves the draw list in the tick that disposes the renderer.

## Voxel renderer cleanup

A client voxel rebuild builds child chunk entities for opaque, masked, and
transparent renderers. When a dirty chunk rebuilds or empties, it queues its
previous renderers into `MeshRendererCleanup.Pending`
(`WaywardBeyond.Client/Voxels/Building/VoxelEntityBuilder.cs:258-360`).

`CleanupMeshRendererSystem` processes pending replacements every tick
(`WaywardBeyond.Client/Systems/CleanupMeshRendererSystem.cs:8-22`). It disposes
the previous renderer and its mesh only after the replacement renderer is bound
(`MeshRenderer.IsBound`, `Swordfish/Graphics/MeshRenderer.cs:13`). If the replacement
fails to bind or the chunk emptied, an age threshold disposes the renderer.
This prevents the chunk mesh from flickering out of view during rebuilds.

`ClientCleanupSystem` drains all queued and pending renderers before it frees a
world entity (`WaywardBeyond.Client/Systems/ClientCleanupSystem.cs:57-60`).

`MeshRendererCleanup.DisposeAll` owns the full drain loop
(`WaywardBeyond.Client/Components/MeshRendererCleanup.cs:30-38`).

## Jolt shape wrappers

`JoltPhysicsSystem.TryGetJoltShape` creates managed wrappers for native Jolt
shapes (`Swordfish/Physics/Jolt/JoltPhysicsSystem.cs:204-251`). The wrappers are
temporary. Jolt keeps its own reference after `SetShape` or `CreateBody`.

The caller disposes the shape wrapper after `SetShape` and after `CreateBody`
(`JoltPhysicsSystem.cs:335-340`, `:349-364`). A compound build disposes the
compound settings and each child wrapper before it returns the compound wrapper
(`JoltPhysicsSystem.cs:212-241`).

## Tests

`Swordfish.Tests/MeshRendererCleanupTests.cs` pins both queue drains:

- `CleanupTickDisposesQueuedRenderersAndMeshes`
- `ClientCleanupDrainsQueuedRenderersBeforeFreeing`

Each test asserts that the renderer, its mesh, and the queue are gone after the
tick.
