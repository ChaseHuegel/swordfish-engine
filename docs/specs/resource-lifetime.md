# Resource Lifetime: Native Component Resources

One subject: who disposes a native renderer or physics resource, and on which
thread. The voxel edit paths that create these resources are in
[networking-voxel-edits](networking-voxel-edits.md).

## Ownership rule

The engine owns the release of native resources held by components. Game code
adds and removes components and does not dispose the resource by hand during
play. A system `Dispose` may still release a resource that it owns at shutdown,
because no tick follows to observe the removal. Engine processors query removed
components and release the captured value:

| Handler | Component | Runs in |
|---|---|---|
| `PhysicsComponentProcessor` | `PhysicsComponent` | `JoltPhysicsSystem.Tick` |
| `MeshRendererComponentProcessor` | `MeshRendererComponent` | `ResourceLifetimeSystem` |
| `DataStore.DisposeRemoved` | Any `IDisposable` component (e.g. `MeshBatchRendererComponent`) | `ResourceLifetimeSystem` |

A component that only needs immediate release on `Remove` or `Free` implements
`IDisposable`. `DataStore.DisposeRemoved()` disposes its captured removal
without boxing and needs no custom processor or DI registration. Components
that require a specific thread/lock or deferred replacement tracking use
`ComponentResourceProcessor<T>` (`Swordfish/ECS/ComponentResourceProcessor.cs:7`)
and do not implement `IDisposable`. It queries `QueryRemoved<T>`, calls
`OnRemoved`, and then acknowledges the removal with `ClearRemoved<T>`.

`JoltPhysicsSystem` owns the physics processor and runs it at the start of
every tick, on the physics thread, before the solver, and under the solver
lock. The body is released synchronously with `PhysicsComponent.DisposeBody`
(`Swordfish/ECS/PhysicsComponent.cs:64`). The removed value is captured by the
store, so the server's same-tick leave and rejoin cannot overwrite the old body.

`ResourceLifetimeSystem` (`Swordfish/ECS/ResourceLifetimeSystem.cs:6`) ticks the
registered processors and calls `store.DisposeRemoved()` on the ECS thread.
`EngineContainer` registers the processors and the system.

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

## Mesh ownership

`Mesh.Shared` (`Swordfish/Graphics/Mesh.cs:16`) states whether a mesh is shared
across renderers or owned by a cache. It defaults to `false`. A non-shared mesh
is disposed when its renderer retires. A shared mesh outlives its renderers, so
retirement disposes the renderer and leaves the mesh alone.
`MeshRendererComponentProcessor.Retire` honors the flag
(`Swordfish/Graphics/MeshRendererComponentProcessor.cs:234`).

- `MeshDatabase` marks every loaded `.obj` mesh `Shared = true`
  (`Swordfish/IO/MeshDatabase.cs:27`).
- `BillboardSystem` marks its shared quad mesh `Shared = true`.
- `Swordfish.Demo` marks its shared cube and test meshes `Shared = true`.
- Brick placement previews and procedural brick view models build a mesh per
  renderer and leave `Shared = false`.

`MeshBatchRendererComponent` implements `IDisposable` and disposes both its
`MeshBatchRenderer` and `MeshBatch`.

## Renderer retirement

`MeshRendererComponentProcessor` keeps the renderer that is currently bound to
each entity UUID. When the component's renderer changes, it queues the previous
renderer with its replacement. When the component is removed or its entity is
freed, it queues the renderer alone. A renderer that moves to another live
entity in the same tick is not retired. A queued renderer waits for the
replacement to bind (`IsBound`), or for five ticks, before it is disposed. A
removal without a replacement waits two ticks. `MeshRenderer.Dispose` is
idempotent, and a queued renderer is never queued twice.

`MeshBatchRendererComponent` is disposed by `DataStore.DisposeRemoved()` as
soon as the removal is observed. A voxel batch is never replaced, so it does
not defer.

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

A voxel object owns four `MeshBatch` resources and four `MeshBatchRenderer`
child entities for opaque, non-shadow-casting opaque, masked, and transparent
geometry. A chunk edit updates one range in each batch. An emptied chunk removes
its range. No per-chunk mesh or renderer is created.

`MeshBatchRenderer` disposal posts to the GL synchronization context. The
context disposes the `GLMeshBatch` and its buffers.
`VoxelEntityBuilder.FreeChunks` frees every child entity
(`WaywardBeyond.Client/Voxels/Building/VoxelEntityBuilder.cs:328`) and clears
the registry. The engine batch processor releases the renderers and batches on
the next tick. The registry stores each child's `Uuid`, so a recycled entity
index is ignored.

The interaction outline uses `Line` handles from `ILineRenderer`
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
shapes (`Swordfish/Physics/Jolt/JoltPhysicsSystem.cs:239-303`). The wrappers are
temporary. Jolt keeps its own reference after `SetShape` or `CreateBody`.

The caller disposes the shape wrapper after `SetShape` and after `CreateBody`
(`JoltPhysicsSystem.cs:420-424`, `:434-449`). A compound build builds a
`StaticCompoundShape` (or an empty `MutableCompoundShape` when empty) and
disposes the compound settings and each deduplicated child wrapper before it
returns the compound wrapper (`JoltPhysicsSystem.cs:239-303`). A convex hull
build disposes its `ConvexHullShapeSettings` before it returns
(`JoltPhysicsSystem.cs:350-369`).

## Jolt bodies

A created body is owned by its `PhysicsComponent`. `JoltPhysicsSystem` releases
a removed body through `PhysicsComponentProcessor` before the solver runs. The
release is idempotent and does not depend on a later tick.

The store captures the removed component value when `Remove` or `Free` clears
it. `QueryRemoved` reads that captured value, so a freed entity index that is
reused in the same tick still reports the previous body.
`ClearRemoved<PhysicsComponent>` acknowledges it so it is handled once.

## Tests

`Swordfish.Tests/ComponentResourceLifetimeTests.cs` pins the engine path:

- `RemovedRendererRetiresAfterRemoval`
- `FreedEntityRendererRetires`
- `ReplacedRendererWaitsForReplacementToBind`
- `ReplacementNeverBoundRetiresAfterAgeThreshold`
- `ReplacementThenFreeInSameTickRetiresBothRenderers`
- `NullRendererReplacementRetiresPreviousRenderer`
- `RendererMovedToAnotherEntityIsNotRetired`
- `ReplacedRendererRevertedBeforeRetirementIsNotDisposed`
- `RendererMovedToLowerIndexEntityBeforeOldEntityIsReplacedIsNotDisposed`
- `NullRendererOnOldEntityKeepsRendererLiveOnNewEntity`
- `SharedMeshIsNotDisposedWhenRendererRetires`
- `ResourceLifetimeSystemResolvesRegisteredProcessorsAndDisposesRemoved`
- `RemovedBatchRendererReleasesRendererAndBatch`
- `RemovedPhysicsComponentDestroysBody`
- `FreedPhysicsBodyIsDestroyedExactlyOnce`
- `DisposingAComponentCopyBeforeTheRemovalIsSafe`
- `RecycledEntityIndexStillDestroysPreviousBody`

`Krill.Tests/DataStoreDirtyTests.cs` pins removal capture:

- `QueryRemovedDetectsRemovalAndDoesNotClear`
- `QueryRemovedClearingDirtyDoesNotAcknowledgeRemoval`
- `QueryRemovedSurvivesRecycledSlot`
- `QueryRemovedSurvivesReAdd`
- `RemoveOfMissingComponentIsNotReported`
- `DisposeRemovedDisposesRemovedDisposableComponentsOnce`
- `DisposeRemovedSurvivesRecycledSlot`

`WaywardBeyond.Tests/BrickPreviewTests.cs` pins the preview's replacement wait:

- `System_ShowsAndHidesThePreviewEntity` covers a removed renderer.
- `System_ReplacedRendererWaitsForTheNewRenderer` covers a replaced renderer.
