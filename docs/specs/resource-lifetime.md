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

`GLOffscreenRenderer` creates temporary FBOs, renderbuffers, a resolve texture,
a model buffer, and per-mesh VAOs inside `RenderBatchToTextures`
(`Swordfish/Graphics/SilkNET/OpenGL/GLOffscreenRenderer.cs:66-136`). It disposes
every temporary GL handle on the GL thread before the batch returns. The caller
owns the returned `Texture` handles and must dispose them when no longer needed.
`ItemIconDatabase` (`WaywardBeyond.Client/Items/ItemIconDatabase.cs`) owns and
disposes the generated brick icon `Texture` and `Material` handles on shutdown,
and `BrickIconRenderer` (`WaywardBeyond.Client/Items/BrickIconRenderer.cs`)
disposes its dedicated icon shader and materials.

## Engine shutdown

The container does not own the window or the GL binding. The window loop unbinds
the GL context when it ends. The engine captures the window context in the
engine container (`EngineContainer.cs:36-52`) and makes it current before the
container disposes (`SwordfishEngine.cs:134-146`). The container then deletes its
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
(`WaywardBeyond.Client/Components/MeshRendererCleanup.cs`). The brick placement
preview uses the same queue for replaced and hidden preview renderers
(`WaywardBeyond.Client/Systems/BrickPreviewSystem.cs:91`, `:114`). The
interaction outline uses `Line` handles from `ILineRenderer`
(`WaywardBeyond.Client/Debug/MeshOutline.cs`). The interaction service keeps
them for the process lifetime.

## Jolt debug renderer

`JoltDebugRenderer` owns two streaming vertex array objects, one shader program,
and one native `FrustumBodyDrawFilter` (`Swordfish/Graphics/Jolt/JoltDebugRenderer.cs:18-57`).
The engine container disposes the singleton on shutdown while the GL context is
current. `DisposeManaged` frees the GL handles and the body filter before
`NativeObject` frees the native debug renderer (`JoltDebugRenderer.cs:141-148`).

## Jolt shape wrappers

`JoltPhysicsSystem.TryGetJoltShape` creates managed wrappers for native Jolt
shapes (`Swordfish/Physics/Jolt/JoltPhysicsSystem.cs:214-344`). The wrappers are
temporary. Jolt keeps its own reference after `SetShape` or `CreateBody`.

The caller disposes the shape wrapper after `SetShape` and after `CreateBody`
(`JoltPhysicsSystem.cs:390-399`, `:404-424`). A compound build builds a
`StaticCompoundShape` (or an empty `MutableCompoundShape` when empty) and
disposes the compound settings and each deduplicated child wrapper before it
returns the compound wrapper (`JoltPhysicsSystem.cs:226-267`). A convex hull
build disposes its `ConvexHullShapeSettings` before it returns
(`JoltPhysicsSystem.cs:325-344`).

## Tests

`WaywardBeyond.Tests/MeshRendererCleanupTests.cs` pins both queue drains:

- `CleanupTickDisposesQueuedRenderersAndMeshes`
- `ClientCleanupDrainsQueuedRenderersBeforeFreeing`

Each test asserts that the renderer, its mesh, and the queue are gone after the
tick.

`WaywardBeyond.Tests/BrickPreviewTests.cs` pins the preview's use of the same queue:

- `System_ShowsAndHidesThePreviewEntity` covers a hidden renderer.
- `System_ReplacedRendererWaitsForTheNewRenderer` covers a replaced renderer.
