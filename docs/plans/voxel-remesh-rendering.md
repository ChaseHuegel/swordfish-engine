# Plan: Voxel remesh and chunk rendering

- Type: plan
- Status: proposed
- Workflow: [../specs/issues.md](../specs/issues.md)
- Related: [0086](../issues/0086-incremental-voxel-rebuild.md), [0095](../issues/0095-voxel-light-geometry-split.md), [0096](../issues/0096-per-chunk-compound-collision.md)
- Batching proposal: [../proposals/voxel-chunk-batching.md](../proposals/voxel-chunk-batching.md)

## How to use this plan

This document is the source of truth for the work order. Keep it current.

- Mark a step `[x]` only when the code, the tests, and the docs for that step
  are complete.
- Add the commit hash and the date after every completed phase.
- If the implementation differs from a step, rewrite the step and record why.
  Do not leave a stale step behind.
- Do not mark a phase complete until its gate passes. A gate is the build, the
  affected test suites, and the docs pass.
- Refresh every `file:line` pointer when the code moves.
- Engine phases land first. Game phases stack on the engine commits.
- One commit is one subject. Do not mix engine and game in one commit.

## Goal

Make voxel edits fast and make chunk rendering cheap without changing the
gameplay contract.

1. Fix the ambient light defects that make open air and holes dark.
2. Make the full build and the incremental edit agree.
3. Stop the renderer from drawing the whole visible world 18 times per frame.
4. Keep the per-edit rebuild cost proportional to the edit.
5. Add a batched chunk renderer that removes the per-chunk draw call.

## Working rules

- Follow [development](../development.md) and the review lens in
  [code-review](../specs/code-review.md).
- No new `TODO`, `FIXME`, or `HACK`. Capture work as an issue.
- Expected failure returns `Result<T>`. Do not throw for a case the caller can
  handle.
- The client ECS ticks on the `"ECS"` thread. The renderer runs on the GL
  thread. Do not call GL from the ECS thread.
- Every behavior change gets a test. Every performance claim gets a number.
- Keep the docs index and the affected specs current.

## Decisions

| ID | Decision | State | Note |
|---|---|---|---|
| D1 | Voxel gizmo toggle key | closed | `ShortcutService` fires every shortcut with a matching key (`Swordfish/Input/ShortcutService.cs:64-85`). Recommended: use `F2` for the gizmo toggle. |
| D2 | Ambient model v2 | proposed | Keep `XY`, `XZ`, and `ZY`. Seed every ambient cell in the full build. Do not decay into an ambient cell. See Phase G1. |
| D3 | Chunk edge | closed | The chunk edge is 16. Pin the generator and the save default. Do not raise it to work around draw calls. |
| D4 | Batched chunk renderer | proposed | See the [batching proposal](../proposals/voxel-chunk-batching.md). |
| D5 | Collision follow-up | closed | One body per voxel object with per-chunk compound children. See [0096](../issues/0096-per-chunk-compound-collision.md). |
| D6 | Transparent ordering | open | See "Transparent ordering" below. |
| D7 | `VoxelLightingService` singleton | closed | Leave as-is. Do not add a follow-up. |

## Baseline

Record these before Phase E0. Add the numbers to this table.

| Metric | Value | How |
|---|---|---|
| Build | clean | `dotnet build` |
| `Swordfish.Tests` | 435 pass, 1 unrelated flaky transport test | `dotnet test Swordfish.Tests` |
| `WaywardBeyond.Voxels.Tests` | 26 pass | `dotnet test WaywardBeyond.Voxels.Tests` |
| Full-build open-air light at `y = 1..15` | 15, 14, 13, …, 1 | Probe |
| Trench floor light | 13 | Probe |
| One lamp dirty chunks | 10 | Probe |
| Draw calls per frame | TODO | Performance overlay after D1 and Phase E3 |
| Remesh ms per edit | TODO | Benchmark after Phase G3 |

## Commit order

| Order | Subject | Module | Depends on |
|---|---|---|---|
| E0 | Tick child transforms before the render context | Swordfish | — |
| E1 | Make camera culling view-only | Swordfish | E0 |
| E2 | Cull shadow casters per pass and layer point shadows | Swordfish | E1 |
| E3 | Cutout depth prepass, single depth pass, draw-call count | Swordfish | E1 |
| E4 | Remove per-frame renderer overhead | Swordfish | E1–E3 |
| E5 | Quantize shape equality and finish the renderer docs | Swordfish | — |
| E6a | Add the batched mesh data model | Swordfish | E4 |
| E6b | Add the OpenGL multi-draw batch stage | Swordfish | E6a |
| E6c | Add batch lifetime and scene integration | Swordfish | E6b |
| G1 | Replace the ambient model and unify light paths | WaywardBeyond | E commits |
| G2 | Add light-only chunk dirtiness | WaywardBeyond | G1 |
| G3a | Add the chunk occupancy mask | WaywardBeyond | G1 |
| G3b | Reuse rebuild scopes and buffers | WaywardBeyond | G3a |
| G3c | Make the chunk entity lifetime single-owner | WaywardBeyond | — |
| G3d | Add the rebuild time budget and system order | WaywardBeyond | G3c |
| G3e | Remove per-chunk mesh allocation | WaywardBeyond | G3b |
| G4 | Gate the voxel gizmo behind a debug toggle | WaywardBeyond | D1 |
| G5 | Remove dead code, add the benchmark, run the docs pass | WaywardBeyond | G1–G4 |
| G6 | Adopt the batched renderer for chunks | WaywardBeyond | E6, G2, G3 |
| V | Verify and close | both | all |

## Phase checklist

- [x] E0 Child transform order
- [x] E1 View-only culling
- [ ] E2 Shadow culling and layered point shadows
- [x] E3 Cutout depth, single depth pass, draw-call count
- [x] E4 Per-frame renderer overhead
- [ ] E5 Shape equality and helper docs
- [ ] E6a Batched mesh data model
- [ ] E6b Multi-draw batch stage
- [ ] E6c Batch lifetime and fallbacks
- [ ] G1 Ambient model and unified light paths
- [ ] G2 Light-only chunk dirtiness
- [ ] G3a Occupancy mask
- [ ] G3b Rebuild scope and buffer reuse
- [ ] G3c Chunk entity lifetime
- [ ] G3d Rebuild time budget and system order
- [ ] G3e Mesh allocation
- [ ] G4 Voxel gizmo toggle
- [ ] G5 Dead code, benchmark, docs pass
- [ ] G6 Batched chunk adoption
- [ ] V Final verification

## Progress log

Keep this table current. Add the commit hash and the date when a phase lands.

| Phase | State | Commit | Date | Note |
|---|---|---|---|---|
| E0 | done | pending | 2026-10-31 | ChildSystem now ticks before GLRenderContext |
| E1 | done | pending | 2026-10-31 | Scene holds all renderers; stages cull per pass |
| E2 | not started | — | — | |
| E3 | done | pending | 2026-10-31 | Masked depth uses cutout; one depth pass; shadow count included |
| E4 | done | pending | 2026-10-31 | One stage pre-render, cached targets/state, reused scene buffers |
| E5 | not started | — | — | |
| E6 | not started | — | — | |
| G1 | not started | — | — | |
| G2 | not started | — | — | |
| G3 | not started | — | — | |
| G4 | not started | — | — | |
| G5 | not started | — | — | |
| G6 | not started | — | — | |
| V | not started | — | — | |

## Phase E0: Tick child transforms before the render context

Commit: `Tick child transforms before the render context`.

Problem: `GLRenderContext` is registered at
`Swordfish/EngineContainer.cs:51-53`. `ChildSystem` is registered at `:84`.
Systems tick in registration order (`Swordfish.ECS/World.cs:37-43`). A chunk
child uses the transform from the previous tick. On the first tick after
creation the chunk transform is still identity, so the structure renders and
culls at the world origin.

Steps:

- [x] Register `ChildSystem` before `GLRenderContext`.
- [x] Add a test that the render context sees the child transform written in the
      same tick. Use a fake render context or a system order test.
- [x] Update `docs/architecture.md` if it lists the system order.

Gate: build, `Swordfish.Tests`.

## Phase E1: Make camera culling view-only

Commit: `Make renderer culling view-only`.

Problem: `GLRenderContext.Tick` removes off-screen entities from the scene
(`Swordfish/Graphics/SilkNET/OpenGL/GLRenderContext.cs:111-119, 404-418`). The
shadow pass then draws only camera-visible chunks
(`Swordfish/Graphics/SilkNET/OpenGL/Renderers/GLInstancedRenderer.cs:60-95,
280-380`). Objects outside the camera view cast no shadow.

Steps:

- [x] Add the world-space AABB to `EntityModel`
      (`Swordfish/Graphics/SilkNET/OpenGL/Renderers/EntityModel.cs`). Compute it
      in the scene build. Do not recompute it in every pass.
- [x] Add `Frustum` to `RenderScene` (`Swordfish/Graphics/RenderScene.cs`).
- [x] Stop culling in `GLRenderContext.Tick`. Add every live mesh renderer to
      the scene.
- [x] Cull inside `GLInstancedRenderer`. Use `renderScene.Frustum` for camera
      passes. Build a frustum from `view * projection` for every shadow pass.
- [x] Key the instance lists by pass. The shadow pass must use the unculled
      caster list.
- [x] Keep the unbound queue and the deferred disposal behavior intact.
- [x] Add tests:
  - [x] `BoundingBox.Transform` and `BoundingBox.Union` cover the world bounds.
  - [x] `FrustumCullingTests` covers off-camera rejection and partial intersection.
  - [x] manual shadow check: a caster behind the camera must still cast.
- [x] The scene build no longer culls. Every live renderer reaches the stage.
- [x] The stage culls each target per pass. The shadow pass uses its own frustum.
- [x] Update the rendering docs.

Gate: build, `Swordfish.Tests`, `Reef.Tests`.

## Phase E2: Cull shadow casters per pass and layer point shadows

Commit: `Cull shadow casters per cascade and layer point shadows`.

Problem: `RenderShadows` calls `DrawShadowPass` once per cascade and once per
cube face (`Swordfish/Graphics/SilkNET/OpenGL/Pipelines/ForwardPlusRenderingPipeline.cs:580-785`).
Every call draws every visible chunk. The defaults are 3 cascades and 2 point
shadows (`Swordfish/Settings/ShadowSettings.cs:8-13`). That is 15 full-scene
passes before the depth and color passes start.

Steps:

- [x] Cull each caster against the current cascade or cube-face frustum.
      Implemented in `RenderShadowPass` in the E1 commit.
- [ ] Do not cache the caster list across frames unless the scene version, the
      camera, and the light are unchanged. Voxel objects are physics bodies and
      can move.
- [ ] Add a layered point-shadow shader. Write `gl_Layer` in a geometry shader.
      Draw one pass per point light for all 6 faces. Blocked on GPU
      verification. Keep the per-face path until the layered path is proven.
- [x] Keep the per-face path in place until the layered path is verified on the
      target drivers.
- [x] Add a unit test for the cascade cull predicate.
- [ ] Manual check with the shadow debug view.
- [ ] Update `docs/specs/rendering-shadows.md`.

Gate: build, `Swordfish.Tests`, manual shadow check.

## Phase E3: Cutout depth prepass, single depth pass, draw-call count

Commit: `Fix masked depth and count every draw call`.

Problem: the depth pass skips material setup, so masked chunks draw with
`_depthShader` (`Swordfish/Graphics/SilkNET/OpenGL/Renderers/GLInstancedRenderer.cs:202-252`).
The SSAO half-res depth pass and the full depth pass both draw the scene
(`Swordfish/Graphics/SilkNET/OpenGL/Pipelines/ForwardPlusRenderingPipeline.cs:334-413`).
`RenderShadows` discards the draw-call result (`:640, 766`).

Steps:

- [x] Bind `depthCutoutShader` or `depthCutoutArrayShader` for the masked
      bucket in a depth pass. Set `uAlphaCutoff` and `texture0`.
- [x] Render the full depth buffer first. Use it for SSAO. Remove the half-res
      depth draw, or downsample the full depth into the SSAO input.
- [x] Set `_preDepthTex` or replace its users with `_depthTex`.
- [x] Return the shadow draw-call count from `RenderShadows` and add it to the
      pipeline total.
- [ ] Manual: verify the overlay count against the real calls.
- [x] Update `docs/specs/resource-lifetime.md` if the pass order changes.

Gate: build, `Swordfish.Tests`, manual SSAO and cutout check.

## Phase E4: Remove per-frame renderer overhead

Commit: `Remove per-frame renderer overhead`.

Problem: `PreRender` runs 4 times per frame and rebuilds 3 dictionaries each
time. Every lookup hashes a `GLRenderTarget`, which hashes `RenderOptions` by
reflection (`Swordfish/Graphics/SilkNET/OpenGL/GLRenderTarget.cs:93-102`).
`lastRenderOptions.Value.Equals(...)` boxes per target per pass
(`GLInstancedRenderer.cs:243-251`). `GLRenderer` binds every visible renderer
every frame (`Swordfish/Graphics/SilkNET/OpenGL/GLRenderer.cs:96-100`).
`GLRenderContext` allocates a scene array every tick (`:118`).

Steps:

- [x] Add `IEquatable<RenderOptions>` with a manual `Equals` and `GetHashCode`
      (`Swordfish/Graphics/RenderOptions.cs`).
- [x] Cache the render-options hash in `GLRenderTarget`.
- [x] Replace `Materials.Any(...)` with a cached `IsTransparent` flag. Remove
      the LINQ call (`GLInstancedRenderer.cs:68`).
- [x] Cache the bound target on the renderer as `IRenderTarget?`
      (`Swordfish/Graphics/MeshRenderer.cs`). Set it in `GLRenderContext` on
      bind. Use it in `PreRender` and the scene build. Do not leak
      `GLRenderTarget` into the renderer-agnostic type. Remove the per-entity
      `TryGetRenderTarget` dictionary lookup.
- [x] Delete the per-frame `Bind` loop in `GLRenderer`. The unbound queue
      already binds every renderer once.
- [x] Build the instance lists once per frame. Reuse them for depth and color.
- [x] Double-buffer the scene array. Remove `_renderableEntities.ToArray()`.
- [x] Use the cached model upload for one-instance targets in the transparent
      path (`GLInstancedRenderer.cs:187-195`).
- [x] Back `MeshRenderer.IsBound` with a `volatile` field. A property cannot be
      `volatile`.
- [x] Add tests for `RenderOptions` equality and hash, and for the scene buffer
      reuse.
- [x] Add the render-target cache to the resource lifetime doc.

Gate: build, `Swordfish.Tests`, draw-call and frame-time check.

## Phase E5: Quantize shape equality and finish the renderer docs

Commit: `Quantize shape equality and document the renderer helpers`.

Problem: `Shape.Equals` uses a `1e-5` tolerance (`Swordfish.Library/Types/Shapes/Shape.cs:87-119`).
`Shape.GetHashCode` uses the exact values (`:127-145`). Equal shapes can hash
differently, so the Jolt shape cache misses.

Steps:

- [ ] Add one quantizer. Use the same step in `Equals` and `GetHashCode` for
      every float field. Quantize both sides.
- [ ] Add tests: values inside the step are equal and hash equal. Values
      outside the step are not equal.
- [ ] Add the one-line summary to `VectorExtensions.ApproximatelyEquals`.
- [ ] Do not change the `ShapeType` switch.
- [ ] Run the docs pass for the changed engine doc.

Gate: build, `Swordfish.Tests`.

## Phase E6: Batched chunk rendering

Read the [batching proposal](../proposals/voxel-chunk-batching.md) before the
first step. Do not start E6 until D4 is approved.

### E6a: Batched mesh data model

Commit: `Add the batched mesh data model`.

- [ ] Add `MeshBatch`, `MeshRangeHandle`, and `MeshBatchWriter` to
      `Swordfish/Graphics`.
- [ ] Support `AddRange`, `UpdateRange`, `RemoveRange`, and `Commit`.
- [ ] Add slot allocation, growth, and compaction.
- [ ] Keep the vertex format identical to `Mesh`.
- [ ] Add unit tests for add, update, remove, growth, and compaction.
- [ ] Document the contract in `docs/specs/resource-lifetime.md`.

### E6b: OpenGL multi-draw batch stage

Commit: `Draw batched meshes with multi-draw`.

- [ ] Add `GLMeshBatch` and `GLMeshBatchRenderer`.
- [ ] Add the batch render stage. Implement `PreRender`, `Render`, and
      `RenderShadowPass`.
- [ ] Draw with `glMultiDrawElementsBaseVertex`. Use one draw per material per
      pass.
- [ ] Cull each range against the pass frustum.
- [ ] Sort transparent ranges back to front.
- [ ] Upload changed ranges with `BufferSubData`. Never create a GL object per
      range.
- [ ] Add the `IRenderContext.Bind` path and the scene integration. Generalize
      `UnboundRenderer` or add a second queue so the GL thread can bind a batch
      renderer without a `MeshRenderer`.
- [ ] Add tests for the command list and the transparent order.
- [ ] Update `docs/specs/resource-lifetime.md`.

### E6c: Batch lifetime and fallbacks

Commit: `Complete batched mesh lifetime`.

- [ ] Dispose batches on the GL thread.
- [ ] Handle a batch that grows while a frame references it.
- [ ] Handle an empty batch and a removed range.
- [ ] Add the fallback path when `glMultiDrawElementsBaseVertex` is absent.
- [ ] Add a benchmark for 1,000 ranges with 10 percent visible.
- [ ] Update the docs.

Gate for E6: build, `Swordfish.Tests`, `Reef.Tests`, benchmark, manual draw
check.

## Phase G1: Replace the ambient model and unify light paths

Commit: `Fix ambient light seeding and unify light paths`.

### Ambient model v2

The model keeps the three projection maps. It changes the predicate, the seed
set, and the propagation rule.

**Predicate**

```
openY = no XZ entry, or y < min, or y > max
if (openY) return true

openZ = no XY entry, or z < min, or z > max
openX = no ZY entry, or x < min, or x > max
enclosedY = XZ entry and min < y < max
enclosedZ = XY entry and min < z < max
enclosedX = ZY entry and min < x < max

if (openZ && (!enclosedX || !enclosedY)) return true
if (openX && (!enclosedY || !enclosedZ)) return true
return false
```

Why `openY` is unconditional: a cell with no solid in its whole vertical column
is open to space above or below. The X and Z checks keep the decay in tunnels
and cavities. The three maps stay. The result is asymmetric on purpose. Record
this in `docs/specs/voxels.md`.

**Full build**

- [ ] Iterate every resident air cell. If `IsAmbientSource` returns true, set
      the light to 15 and enqueue it. Replace the three shell loops in
      `AmbientLightPass` with this volume pass.
- [ ] Keep the light-source seed pass.

**Propagation**

- [ ] In both `LightPropagationPass` and `VoxelLightingService`, set the next
      level to `IsAmbientSource(destination) ? 15 : light - 1`.
- [ ] This stops open air from decaying and re-seeds a region that an edit
      opens.

**Incremental edits**

- [ ] Update the depth state first. Then re-evaluate the edited cell and its
      six neighbours.
- [ ] When a culler becomes a non-culler, use
      `IsAmbientSource ? 15 : maxNeighbour - 1`.
- [ ] Remove the `adjacentToSky` non-resident special case. Use the ambient
      predicate as the only authority.
- [ ] When a write creates a new resident chunk, queue its air cells for
      ambient evaluation. Do not leave them at 0.
- [ ] Keep the existing removal queue behavior.

### Proposal for 1.4 (residency-dependent light)

Make residency explicit. Do not leave it as a hidden read fallback.

- [ ] Add `VoxelObject.IsResident(x, y, z)`.
- [ ] Treat a non-resident cell as open space in `IsAmbientSource`. A missing
      chunk is exterior by definition in a sparse world.
- [ ] When a chunk becomes resident, seed its ambient air. This is the step that
      removes the "dark until touched" chunk.
- [ ] Keep the out-of-bounds read at light 15. Document it as "exterior air
      reads as full ambient". Do not use non-resident reads as gameplay input.
- [ ] Add a test: an edit that creates a chunk at the object boundary produces
      the same light as a full reload.

### Test updates

- [ ] Update the tests that assume open-air decay. A lamp in open space lights
      its ambient neighbours to 15, not 14. This affects
      `ChunkBasedVoxelRemeshingTests.LocalizedLightingAddition_PropagatesAndMarksDirty`
      and any other test with an open-air emitter.
- [ ] Re-baseline `SingleVoxelEdit_ExecutionTimeUnderOneMillisecond`. The new
      predicate adds up to three map lookups per propagation step. Keep the
      budget meaningful, or record the new number and raise the bound with a
      comment.

### Tests

- [ ] Open air above a flat floor is 15 at every height.
- [ ] A 1x1 hole in a 1-thick floor is 15.
- [ ] A 1-wide trench with walls is 15.
- [ ] A pit in a thick slab is 15.
- [ ] A sealed room is 0.
- [ ] A one-mouth tunnel decays with distance.
- [ ] A through-tunnel decays from both mouths.
- [ ] The underside of a floating slab is 15.
- [ ] The side of a large cube is 15 at mid-height.
- [ ] An incremental edit sequence equals a full build of the same state for
      every resident voxel.
- [ ] A chunk created by an edit matches a reload.

Gate: build, `Swordfish.Tests`, `WaywardBeyond.Voxels.Tests`, docs.

## Phase G2: Add light-only chunk dirtiness

Commit: `Skip collision rebuild for light-only chunks`.

Problem: `DrainAddQueue` marks every chunk that receives light dirty
(`WaywardBeyond.Client/Voxels/Processing/VoxelLightingService.cs:321-323`).
`RebuildDirtyChunks` remeshes and rebuilds collision for each chunk
(`WaywardBeyond.Client/Voxels/Building/VoxelEntityBuilder.cs:216-230`). One lamp
dirtied 10 chunks in the probe.

Steps:

- [ ] Add a dirty kind to `VoxelChunkRegistry`: `Geometry`, `Light`, or both.
- [ ] Geometry edits mark both. Light writes mark light only.
- [ ] In `RebuildDirtyChunks`, skip `BuildChunkCollision` and
      `ReconcileVoxelEntities` for a light-only chunk.
- [ ] Keep the mesh rebuild for now. It reads the new vertex light.
- [ ] Add a test: a lamp edit changes no chunk collision. A geometry edit does.
- [ ] Record the full split in [0095](../issues/0095-voxel-light-geometry-split.md).

Gate: build, tests, benchmark delta.

## Phase G3: Rebuild performance

### G3a: Chunk occupancy mask

Commit: `Add the chunk occupancy mask`.

The mask is a non-empty bitset. A 16-cubed chunk needs 64 `ulong` values, or
512 bytes. Do not keep an index list per chunk. Most chunks are dense, so an
index list would cost more memory than it saves.

- [ ] Add a lazy `NonEmptyMask` to `ChunkData`.
- [ ] Build it on chunk creation and on load.
- [ ] Update the bit in `ChunkVoxel.Set`.
- [ ] Add `GetOccupiedSampler()`. Use it in `BuildChunk` and
      `BuildChunkCollision`.
- [ ] Add a test: a sparse chunk visits only non-empty voxels. A dense chunk
      produces the same mesh and collision as today.
- [ ] Benchmark the remesh cost.

Note: tracking empty voxels does not help. The loop already skips empty cells in
one branch. The cost is the six-neighbour sample for every non-empty voxel, so
the fast path needs a non-empty index. The bitset gives that path with bounded
memory. If the bitset is rejected, the fallback is a `List<int>` of non-empty
indices kept only for sparse chunks (under 25 percent non-empty). Do not keep a
list for dense chunks.

### G3b: Reuse rebuild scopes and buffers

Commit: `Reuse chunk rebuild scopes and buffers`.

- [ ] Open one DI scope per `Rebuild`, not one per chunk.
- [ ] Resolve `MeshPostPass`, `VoxelEntityPostPass`, and the state objects once.
- [ ] Clear `MeshState`, `EntityState`, and `CollisionState` between chunks.
      Fix `MeshState.Clear` to clear `Masked`.
- [ ] Delete the dead `Build` and `BuildCollision` paths. See G5.
- [ ] Add a test for two sequential chunk builds with one scope.

### G3c: Single-owner chunk entity lifetime

Commit: `Make the chunk entity lifetime single-owner`.

- [ ] Audit every chunk entity free. The current sites are
      `VoxelEntityBuilder.UpdateRendererBucket`, `RemoveBucket`, and
      `ClientCleanupSystem`.
- [ ] Store `(int Entity, Uuid Uuid)` in every registry bucket. Validate the
      Uuid before you touch an entity.
- [ ] Make `VoxelEntityBuilder` the only allocator and freer of chunk entities.
- [ ] Add `FreeChunks(store, entity)` to `VoxelEntityBuilder`. Free every bucket
      entity and clear the registry.
- [ ] Initialize the chunk child's `TransformComponent` from the parent
      transform at creation, not only through `ChildSystem`. This removes the
      identity-transform frame.
- [ ] Call `FreeChunks` before a parent voxel entity is freed:
      `ClientCleanupSystem` and the snapshot removal in `ClientReconcileSystem`.
- [ ] Do not rely on `store.Free` being idempotent. It is not
      ([0039](../issues/0039-datastore-free-idempotency.md)).
- [ ] Add tests:
  - [ ] freeing a parent frees every chunk entity.
  - [ ] a recycled entity index is not touched by a stale bucket.
  - [ ] a chunk that empties frees its entity once.

### G3d: Rebuild time budget and system order

Commit: `Budget voxel rebuild time per tick`.

- [ ] Add `VoxelSettings` with `RebuildBudgetMs = 10` (configurable, TOML,
      `voxel.toml`).
- [ ] Register it with `RegisterConfig`.
- [ ] Add it to `ConfigManager.ApplySettings`.
- [ ] `Rebuild` returns whether it finished. Process chunks until the budget is
      spent. Leave the rest in `DirtyChunks`.
- [ ] `VoxelEntityRebuildSystem` clears the dirty flag only when the rebuild
      finished.
- [ ] Combine the cached chunk colliders and replace the `ColliderComponent`
      once per tick, after the budget batch. Do not rebuild the Jolt body once
      per chunk.
- [ ] Register `VoxelEntityRebuildSystem` after `PlayerInteractionService` so a
      local click rebuilds on the same tick.
- [ ] Add a test: the budget yields after N chunks. The remainder rebuilds on
      the next tick.

### G3e: Remove per-chunk mesh allocation

Commit: `Reduce chunk mesh allocation`.

- [ ] Compute chunk mesh bounds from the chunk coordinates, not from the
      vertices.
- [ ] Add a pooled `MeshState` builder. Reuse the lists and arrays.
- [ ] Remove the `ToArray()` copies. Upload spans or reuse arrays.
- [ ] Pool the per-chunk `Mesh` and buffer objects. Keep the deferred disposal
      for the GL handles. Delete this pool in G6 when the batch renderer takes
      over.
- [ ] Add a GC allocation benchmark.

Gate for G3: build, tests, benchmark, allocation number.

## Phase G4: Gate the voxel gizmo behind a debug toggle

Commit: `Gate voxel gizmos behind a debug shortcut`.

See D1. Resolve D1 first. The recommended key is `F2`.

- [ ] Add `DebugSettings.VoxelGizmosVisible` (default false).
- [ ] Register the shortcut in `DebugOverlayRenderer` with category
      `Developer/Debug` and `IsEnabled = IsVisible`, same as the F4 pattern
      (`WaywardBeyond.Client/UI/DebugOverlayRenderer.cs:46-54`).
- [ ] `OnToggleDebug` sets `VoxelGizmosVisible` to false on every F3 press.
- [ ] `VoxelDebugGizmoSystem` runs only when `OverlayVisible` and
      `VoxelGizmosVisible` are true. Hide the lines otherwise.
- [ ] Add a test for the toggle and the F3 reset.
- [ ] Update the debug docs.

Gate: build, `Swordfish.Tests`, manual F3/F2 check.

## Phase G5: Remove dead code, add the benchmark, run the docs pass

Commit: `Remove dead voxel renderer code`.

- [ ] Remove `VoxelObjectBuilder.Build`, `BuildCollision`, and `Data`
      (`WaywardBeyond.Client/Voxels/Building/VoxelObjectBuilder.cs:17-34, 116-130, 164-171`).
- [ ] Remove `MeshState.Clear` and `EntityState.Clear`.
- [ ] Remove `VoxelChunkComponent` and its write sites. It has no reader.
- [ ] Remove `VoxelEntityBuilder.RebuildAllChunks`. A dirty flag without a dirty
      chunk is a bug. Log it, do not rebuild everything.
- [ ] Remove `VoxelObjectProcessor.Process` and `PostPass` if the compiler says
      they are unused.
- [ ] Verify the chunk edge is 16 in
      `WaywardBeyond.Server/Generation/AsteroidGenerator.cs:24` and
      `WaywardBeyond.Server/Saves/VoxelWorldEntityFactory.cs:61`. Use one
      shared constant. Add a test that the generated chunk edge is 16.
- [ ] Document `SetLight` as 0..15. Add a debug-only assertion.
- [ ] Copy the predicted voxel before `UpdateLighting` in
      `PlayerInteractionService.ApplyVoxelPrediction`
      (`WaywardBeyond.Client/Systems/PlayerInteractionService.cs:402-416`). The
      method rewrites `ShapeLight`. The queued prediction must keep the
      predicted value.
- [ ] Add `WaywardBeyond.Client.Benchmarks` with a remesh benchmark:
      - [ ] full build
      - [ ] interior edit
      - [ ] border edit
      - [ ] light-only edit
      - [ ] collision rebuild
      - [ ] 1,000 chunk object
- [ ] Add `InternalsVisibleTo("WaywardBeyond.Client.Benchmarks")`.
- [ ] Record the before and after numbers in
      [0086](../issues/0086-incremental-voxel-rebuild.md). Uncheck any box that
      the benchmark does not prove.
- [ ] Docs pass:
  - [ ] `docs/specs/voxels.md`: ambient model, dirty kinds, budget, lifetime.
  - [ ] `docs/specs/resource-lifetime.md`: batch and mesh lifetime.
  - [ ] `docs/specs/networking-voxel-edits.md`: rebuild budget.
  - [ ] `docs/specs/rendering-shadows.md`: per-pass culling.
  - [ ] Refresh all `file:line` pointers.
  - [ ] Add the plan and the proposal to `docs/README.md`.

Gate: build, all test suites, benchmark, docs pass.

## Phase G6: Adopt the batched renderer for chunks

Commit: `Render voxel chunks through the mesh batch`.

Do this after E6 and G2. Do not keep both render paths in production.

- [ ] Add three `MeshBatchRenderer` entities per voxel object: opaque, masked,
      transparent.
- [ ] Map `Short3` chunk offsets to `MeshRangeHandle` in `VoxelChunkRegistry`.
- [ ] `BuildChunk` writes ranges through `MeshBatchWriter`. Remove the per-chunk
      `Mesh` and `MeshRenderer`.
- [ ] Keep the collision registry separate.
- [ ] Sort transparent range commands by camera distance.
- [ ] Remove the chunk renderer bucket dictionaries and
      `MeshRendererCleanup` use for chunks. Keep cleanup for other renderers.
- [ ] Delete `VoxelChunkComponent` if G5 did not already remove it.
- [ ] Update the draw-call expectation in the performance overlay test.
- [ ] Delete the interim mesh pool from G3e.
- [ ] Measure: draw calls, frame time, remesh ms, memory.

Gate: build, tests, benchmark, in-game check.

## Transparent ordering (D6)

Expand this before deciding scope.

Current state: the only sort is inside one `GLRenderTarget`
(`Swordfish/Graphics/SilkNET/OpenGL/Renderers/GLInstancedRenderer.cs:187-195`).
Each chunk is its own target with one matrix, so the sort never orders two
chunks. Transparent chunks draw in dictionary order. Adjacent water or glass
chunks can blend in the wrong order. A chunk rebuild can change the order.

Options:

| Option | Scope | Result |
|---|---|---|
| Leave it | out of scope | Same limitation as the old single mesh. Visible seams on overlapping transparent chunks. |
| Sort targets | small, in scope | Sort `_transparentInstances` by camera distance before drawing. Fixes chunk order. Does not sort faces inside a chunk. |
| Sort batch commands | part of G6 | Sort the range draw commands. Needed for the batched renderer anyway. Fixes chunk order. Does not sort faces inside a chunk. |
| Per-triangle order | out of scope | Needs order-independent transparency or per-pixel linked lists. Too large for this branch. |

Recommendation: do "sort targets" now while per-chunk targets exist. Do "sort
batch commands" in G6. Record "per-triangle order" as an issue if transparent
artifacts remain.

## Verification matrix

Run after every gate.

- [ ] `dotnet build`
- [ ] `dotnet test Swordfish.Tests`
- [ ] `dotnet test Reef.Tests`
- [ ] `dotnet test Reef.Text.Tests`
- [ ] `dotnet test WaywardBeyond.Voxels.Tests`
- [ ] `dotnet run --project WaywardBeyond.Client.Benchmarks -c Release`
- [ ] In game: F3 overlay, F2 gizmos, F3 hides them
- [ ] In game: draw-call count before and after E2, E3, E6, G6
- [ ] In game: light probe scene (open air, pit, trench, tunnel, sealed room)
- [ ] In game: shadows from an off-camera caster
- [ ] In game: transparent chunk order
- [ ] In game: edit latency on a large object
- [ ] Docs pass

## Risks

| Risk | Effect | Mitigation |
|---|---|---|
| Ambient model v2 lights a through-tunnel at 15 | Visible bright tunnels | Keep the X/Z enclosure guard. Use the one-mouth tunnel test. Review against the user. |
| Ambient predicate cost per propagation step | Slower light edits | Measure. Cache an ambient bit per cell if needed. |
| Per-cascade culling cost per frame | CPU cost | Measure. Add a scene version cache only if needed. |
| Layered point-shadow shaders on the target driver | Shadow artifacts | Keep the per-face fallback. Verify on the target GPU. |
| Batch growth mid-frame | Cracks or stale ranges | Apply updates before any draw. Test growth. |
| Entity Uuid checks add cost | Hot path cost | The check only runs on rebuild, not per frame. |
| Rebuild budget starves a large object | Slow visual update | Process at least one chunk per tick. Log a warning when a chunk exceeds the budget. |
| Engine/game commit split | Rework | Engine phases land first. The user splits before merge. |

## Out of scope

- Order-independent transparency.
- A GPU-driven culling path with compute shaders. Plan it after E6 if the CPU
  cull is the bottleneck.
- Server-side collision changes beyond [0096](../issues/0096-per-chunk-compound-collision.md).
- Chunk streaming or LOD.
- Any change to the chunk edge other than keeping 16.
- Refactoring the `VoxelLightingService` singleton or its mutable queues.
  Leave it as-is (D7).
