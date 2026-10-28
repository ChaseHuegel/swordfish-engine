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

## Engine shutdown

The container does not own the window or the GL binding. The window loop unbinds
the GL context when it ends. The engine captures the window context in the
engine container (`EngineContainer.cs:36-52`) and makes it current before the
container disposes (`SwordfishEngine.cs:90-104`). The container then deletes its
GL resources. The engine disposes the GL binding and the window afterward. Do
not dispose `GL` from the close handler. The function pointers are unavailable
until the captured context is current. The batch and mesh unbound queues are
drained by `GLRenderer.OnWindowRender` (`GLRenderer.cs:85-96`).

## Voxel renderer cleanup

A voxel object owns three `MeshBatch` resources and three `MeshBatchRenderer`
child entities for opaque, masked, and transparent geometry. A chunk edit
updates one range in each batch. An emptied chunk removes its range. No
per-chunk mesh or renderer is created.

`MeshBatchRenderer` disposal posts to the GL synchronization context. The
context disposes the `GLMeshBatch` and its buffers. `VoxelEntityBuilder.FreeChunks`
disposes the batch renderers and their batches before a parent voxel entity is
freed. The registry stores each child's `Uuid`, so a recycled entity index is
ignored.

`MeshRendererCleanup` still serves non-voxel renderers
(`WaywardBeyond.Client/Components/MeshRendererCleanup.cs`).

## Jolt shape wrappers

`JoltPhysicsSystem.TryGetJoltShape` creates managed wrappers for native Jolt
shapes (`Swordfish/Physics/Jolt/JoltPhysicsSystem.cs:206-251`). The wrappers are
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
