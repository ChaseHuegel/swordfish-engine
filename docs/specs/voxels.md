# Voxels: The Chunked Container, Coordinates, and Sampling

One subject: how a structure stores voxels in memory and how a reader samples
them. Brick identity and the persisted palette live in
[brick-identity](brick-identity.md).

## The container

`VoxelObject` (`WaywardBeyond.Voxels/VoxelObject.cs`) is a sparse, chunked
container. It owns a `Dictionary<Short3, ChunkData>` keyed by chunk offset. It
creates a chunk on the first write that lands in it.

- `Set(x, y, z, voxel)` writes one cell and creates the chunk when absent
  (`VoxelObject.cs:116`).
- `SetLight(x, y, z, lightLevel)` updates the light level of a resident cell
  without modifying palette counts (`VoxelObject.cs:140`).
- `Get(x, y, z)` returns one cell by value (`VoxelObject.cs:241`).
- `Sample(x, y, z)` returns the cell and its six neighbours
  (`VoxelObject.cs:266`).
- `GetChunkInfos()` returns a copy of every resident chunk. The caller owns the
  voxel arrays (`VoxelObject.cs:353`).

A chunk holds `chunkSize³` voxels. `chunkSize` is a power of two and the
constructor rejects any chunk whose `Size`, voxel count, or array does not match
(`VoxelObject.cs:34-43`).

### One thread at a time

A `VoxelObject` has no lock. The client uses one from the `"ECS"` thread. The
server uses one from the `"Server"` thread. Do not touch one instance from two
threads, and take a copy across a thread boundary. The thread split is in
[architecture](../architecture.md). [0082](../issues/0082-test-writes-store-off-thread.md) records a test that
breaks this rule.

### Addressing

A world coordinate maps to a chunk offset and a local coordinate:

| Value | Expression |
|---|---|
| chunk offset | `world >> chunkShift` |
| local coordinate | `world & chunkMask` |
| local index | `x + (y << chunkShift) + (z << chunkShift2)` |

`chunkShift` is `log2(chunkSize)` and `chunkShift2` is twice that. Two's
complement masking handles negative world coordinates directly, so `-1` lands
on the last cell of chunk `-1`.

### Out of bounds

A read outside a resident chunk returns the shared empty voxel. That voxel
carries light level 15, so an absent chunk reads as full ambient
(`VoxelObject.cs:15`).

The container resets that voxel before every export. A write through a returned
reference therefore cannot reach a later read, and a write outside a resident
chunk is discarded.

## Sampling

`VoxelSample` (`WaywardBeyond.Voxels/VoxelSample.cs`) carries the centre voxel
and six neighbour references. It is a `ref struct`, so a sample never allocates.

The two producers do not agree, and a caller must know which one it holds.

| Producer | Neighbour direction | Coordinates |
|---|---|---|
| `ChunkData.GetSampler()` | rotated by the centre voxel's orientation | filled |
| `VoxelObject.Sample(...)` | fixed world axes | left at `default` |

`ChunkData` rotates each offset by the centre voxel's pitch, yaw, and roll
(`ChunkData.cs:43`), so the mesh and collider passes follow a rotated brick.
`VoxelObject.Sample` does not rotate, so the light passes treat every brick as
axis aligned. [0072](../issues/0072-voxel-sample-producers-disagree.md) tracks the split.

Direction names for an unrotated voxel:

| Name | Offset |
|---|---|
| Left | `-X` |
| Right | `+X` |
| Above | `+Y` |
| Below | `-Y` |
| Ahead | `+Z` |
| Behind | `-Z` |

A voxel packs its 6-bit orientation and per-voxel flags into
`Voxel.OrientationFlags` (`WaywardBeyond.Data/Nsd/voxels.nsd:53`).
`OrientationFlags` (`WaywardBeyond.Voxels/OrientationFlags.cs`) unpacks bits
`0..5` as `Orientation` and bit `6` (`0x40`) as `HasFaceOverrides`.

## Per-face paint overrides

Unpainted chunks allocate no override storage. When a face is painted,
`ChunkData` (`WaywardBeyond.Voxels/ChunkData.cs`) lazily allocates a
`Dictionary<int, FaceOverride>` keyed by `(voxelIndex << 3) | (int)face`, where
`face` is a brick-local `BrickFace` (`WaywardBeyond.Voxels/BrickFace.cs`). Each
`FaceOverride` (`WaywardBeyond.Voxels/FaceOverride.cs`) stores a `ushort`
`TexturePaintId` and a `ushort` `ColorPaintId` (`0` means inherit default).

`VoxelObject.Set` clears any face overrides on the overwritten cell and clears
bit `6`. `VoxelChunkRegistry.MarkFaceModified`
(`WaywardBeyond.Client/Voxels/VoxelChunkRegistry.cs:42`) marks the chunk and
orthogonal boundary neighbors mesh-only dirty so face edits skip collision and
lighting passes, and marks `EntityDirtyChunks` so light decorators refresh.
An unpainted face is tinted by its brick's default color
(`Brick.LinearColor`), which is white unless the brick authors `Color`. A
painted face uses the paint instead. See
[asset-definitions](asset-definitions.md#color).
`ConnectedTextureResolver`
(`WaywardBeyond.Meshing/ConnectedTextureResolver.cs:10`)
resolves 4-bit connected-texture adjacency for `CubeMeshBuilder` and
`MeshPostPass`. It connects native bricks and painted faces by comparing
effective resolved surfaces on coplanar neighbor faces. Built-in non-block
shapes (`Slab`, `Stair`, `Slope`, `Column`, `Plate`) connect only to neighbors
with the same shape and compatible orientation (ignoring symmetric yaw on
`Slab`/`Plate` and axis flips on `Column`), and only along edges where the
shape geometry meets flush, including diagonal ramp neighbors for `Slope`
`Front` faces. `MeshPostPass` also culls flush boundary triangles on built-in
non-block shapes when covered by an adjacent culler block or a same-shape
compatible-orientation neighbor (`MeshPostPass.cs:247`). `Custom` meshes remain
unconnected.
`lightedArray.glsl` applies `VertexColor.rgb` as linear albedo and emissive
tint (including bloom `BrightColor`) and `VertexColor.a` as voxel light. The
`bricks_tint` layer masks that color multiplication: a black mask texel keeps
the authored albedo and emissive color, and a white texel applies the full
paint tint. Every albedo layer has a matching tint layer, so painted and
connected textures resolve both from the same texture coordinate.
See [asset-definitions](asset-definitions.md) for authoring.

## Palette maintenance

Each chunk carries a `VoxelPalette` (`WaywardBeyond.Voxels/VoxelPalette.cs`), a
count of each voxel id. The pass pipeline uses the counts to skip a chunk that
cannot contribute (`VoxelPalette.cs:50`). `VoxelExtensions.Set` updates the count
on every write.

Id 0 is the empty voxel. `Only(0)` is true for a chunk that holds no content.

## Chunk remeshing and dirty tracking

Client structures mesh each resident chunk into one range in each of four
`MeshBatch` resources: opaque, non-shadow-casting opaque, masked, and
transparent. Four batch renderer child entities per object hold the batches
(`WaywardBeyond.Client/Voxels/Building/VoxelEntityBuilder.cs:52-82`).

`GLMeshBatchRenderer.CollectTransparent`
(`Swordfish/Graphics/SilkNET/OpenGL/Renderers/GLMeshBatchRenderer.cs:99-148`)
sorts visible `AlphaMode.Blend` ranges back-to-front across chunks by
`MeshRange.SortCenter` and within each chunk by quad and triangle centroid in
batch-local camera space. `VoxelEntityBuilder.GetChunkSortCenter`
(`WaywardBeyond.Client/Voxels/Building/VoxelEntityBuilder.cs:254-262`) sets
each chunk range's sort center to its uniform grid-cell center so adjacent
chunks sort across exact separating planes while keeping tight frustum bounds.
`TransparentRangeSorter`
(`Swordfish/Graphics/TransparentRangeSorter.cs:8`) caches per-range centroids
and re-sorts only when camera movement meets or exceeds
`RenderSettings.TransparentSortThreshold` (`0.25` units by default).
`GLMeshBatch.SortRangeIndicesIfNeeded`
(`Swordfish/Graphics/SilkNET/OpenGL/GLMeshBatch.cs:99-130`) uploads only the
sorted index slice while keeping the vertex buffer static.
`GLMeshBatchRenderer.RenderTransparentItem`
(`Swordfish/Graphics/SilkNET/OpenGL/Renderers/GLMeshBatchRenderer.cs:151-225`)
renders sorted double-faced ranges in a single pass with face culling disabled.

`VoxelChunkRegistry` (`WaywardBeyond.Client/Voxels/VoxelChunkRegistry.cs`)
tracks range handles, dirty chunk coordinates, cached chunk collision data, and
the batch renderer entities.

A voxel edit marks its resident chunk dirty. An edit on a chunk boundary also
marks the orthogonal neighbor chunk dirty. A light-only write marks
`LightDirtyChunks` and leaves `GeometryDirtyChunks` empty, so the rebuild skips
collision and voxel-entity reconciliation for that chunk.

A rebuild pools a `ChunkBuildContext` (`VoxelObjectBuilder.cs:22-31, 75-119`)
so it reuses the DI scope, `VoxelSurfaceCache`, and mesh, entity, and collision
buffers across edits. `CubeMeshBuilder` precomputes rotated cube faces across
all 64 discrete orientations and resolves surface layers through
`VoxelSurfaceCache` (`WaywardBeyond.Meshing/VoxelSurfaceCache.cs`)
and `FaceOrientationLookup` (`WaywardBeyond.Voxels/FaceOrientationLookup.cs`).
`MeshBatch` (`Swordfish/Graphics/MeshBatch.cs:72-199`) allocates range capacity
headroom and reuses backing arrays so small edits update in place without
triggering arena compaction. A rebuild stops after `VoxelSettings.RebuildBudgetMs`
(10 ms by default). Unprocessed chunks stay dirty and resume on the next tick.
`VoxelEntityBuilder.ReconcileVoxelEntities` tracks chunks with voxel entities in
`VoxelChunkRegistry.ChunksWithEntities` and skips empty chunks.

`VoxelEntityBuilder` is the only owner of the batch renderer child entities.
Each entry stores the entity index and its `Uuid`. A mismatch means the index
was recycled, so the entry is dropped. `VoxelEntityBuilder.FreeChunks` frees the
batch child entities, then clears the registry before a parent voxel entity is
freed. The engine releases each child's renderer and batch from the captured
removal.

`ChunkData.GetOccupiedSampler` visits only non-empty voxels. The chunk keeps a
lazy `ulong` bitmask of non-empty cells. The mask is 512 bytes for a 16-cubed
chunk. `ChunkVoxel.Set` updates the bit when the mask exists.

`VoxelLightingService`
(`WaywardBeyond.Client/Voxels/Processing/VoxelLightingService.cs`) runs
dual-queue BFS flood-fill lighting across resident chunks. The service marks
every modified cell and its chunk dirty. It seeds the edited cell, its six
neighbours, and the air of any chunk an edit adds.

`DepthState`
(`WaywardBeyond.Client/Voxels/Processing/DepthState.cs`) tracks min and max
depth bounds across the XY, XZ, and ZY projection planes in
`VoxelChunkRegistry`. `IsAmbientSource` uses all three planes:

- `openY`: the XZ column has no solid, or `y` is below its min or above its max.
  Open air, pits, holes, and trenches use this path. It seeds light 15.
- `openZ` and `openX`: the cell is outside the span on that axis. The cell
  seeds only when the other two axes do not enclose it. A tunnel or a cavity
  stays between spans, so its light decays from the opening.

`VoxelAmbient.IsAmbient`
(`WaywardBeyond.Client/Voxels/Processing/VoxelAmbient.cs`) adds the residency
rule. A cell on a chunk face that touches a non-resident chunk is exterior. A
newly resident chunk seeds its ambient air after the edit.

`DepthPrePass`
(`WaywardBeyond.Client/Voxels/Processing/DepthPrePass.cs:6-34`) populates
`DepthState` during initial voxel entity construction. The pass filters out
transparent and non-culling bricks so they do not expand depth bounds.

`AmbientLightPass`
(`WaywardBeyond.Client/Voxels/Processing/AmbientLightPass.cs`) scans every
resident air cell. It seeds light 15 for each ambient cell and enqueues it for
propagation. It skips solid cullers and cells that are already at 15.

When a solid culler voxel is placed (`VoxelLightingService.cs`), occluded light
drains through `_removeQueue`. If the placed block replaces open space, the
initial drain light level is 15. During drain removal, brighter neighbors and
equal-decay neighbors re-propagate light via `_addQueue`. At maximum ambient
light (level 15), equal-light neighbors drain unless they are independent light
sources: lamps, ambient cells, or cells at a residency boundary. The service
updates depth bounds via `DepthState.Add` and checks adjacent air cells with
`VoxelAmbient.IsAmbient`.

When a solid culler voxel is deleted (`VoxelLightingService.cs`), the service
updates depth bounds via `DepthState.Remove`. The cell receives light 15 when it
is ambient, or the brightest resident neighbor minus one. Light entering a
cavity or tunnel decays with distance. When the entrance is blocked off, the
decayed light drains to zero through `_removeQueue`, leaving enclosed interiors
dark.

Both light passes use one rule. The next level is 15 when the destination is
ambient, otherwise one less. Open air never decays. A tunnel or a cavity does.

Light bricks (`wb:light`, `wb:small_light`) use `Alpha = Blend`. They are not
cullers, so the mesh pass emits the neighbouring solid's face against the lamp
(`WaywardBeyond.Meshing/MeshPostPass.cs:183-203`). The point
shadow pass uses that face as the occluder instead of the lamp mesh. The lamp
mesh lands in the transparent bucket with `CastShadows = false`
(`WaywardBeyond.Client/Voxels/Building/VoxelMaterials.cs:47-54`), so it does
not self-shadow its own centred point light.

`LightEntityDecorator`
(`WaywardBeyond.Client/Voxels/Building/LightEntityDecorator.cs`) creates a point
light only when at least one orthogonal neighbour is not a culler. It removes
the `LightComponent` when the lamp becomes fully buried. The exposure rule
matches the flood-fill rule. A non-resident neighbour reads as exterior air, so
it counts as exposed.


`VoxelEntityBuilder.RebuildDirtyChunks`
(`WaywardBeyond.Client/Voxels/Building/VoxelEntityBuilder.cs:145-232`) rebuilds
only dirty chunks. Untouched chunks keep their existing ranges.

`ClientVoxelReconcileSystem`
(`WaywardBeyond.Client/Systems/ClientVoxelReconcileSystem.cs:232-252`) reconciles
predicted edits against authoritative server echoes. `VoxelEquals` compares brick
identity, shape, and orientation. It does not compare light level because server
echoes do not compute presentation lighting. This avoids false misprediction
snaps that would clear locally calculated air lighting to zero. `WriteVoxel` also
preserves existing air light when writing an authoritative empty cell.

## Brick placement preview

`BrickPreviewSystem`
(`WaywardBeyond.Client/Systems/BrickPreviewSystem.cs:37`) renders a textured
preview of the brick that the player will place.
`PlayerInteractionService.OnFixedUpdate`
(`WaywardBeyond.Client/Systems/PlayerInteractionService.cs:1207`) publishes the
held brick, effective shape, voxel-local orientation, target cell, and structure
orientation to `BrickPreviewState`. The system ticks after the physics fixed
update on the ECS thread. It stays visible while the game is paused and releases
on menu or loading.

`BrickPreviewMeshBuilder`
(`WaywardBeyond.Meshing/BrickPreviewMeshBuilder.cs:43`) builds one
isolated brick mesh. It reuses `MeshPostPass`, so the preview matches placed
geometry, textures, alpha mode, and custom meshes. The builder bakes the
voxel-local orientation through `BrickExtensions.ToVoxel`, which applies the
`Brick.IsOrientable` rule. The preview transform then applies only the structure
orientation. A non-orientable brick therefore does not rotate, exactly as
placement writes it. The builder bakes light level 15 and casts no shadows.

`PlayerInteractionService` also draws a feature-edge outline around the preview
and around the selected break target (`WaywardBeyond.Client/Debug/MeshOutline.cs`).
The outline uses an outset scale and depth-tested lines, so it sits just outside
the mesh. It replaces the previous triangle-web gizmo.

The system keeps one active `MeshRenderer` for the current brick, shape, and
local orientation. It rebuilds only when that key changes and updates only the
`TransformComponent` otherwise. A replaced or hidden renderer is retired by the
engine's removed-component processor, so the system keeps no cache of meshes for
previous keys or hidden previews. The preview entity carries the `game` tag, so
world teardown frees it. The preview does not collide, emit light, or replicate.

`VoxelMaterials`
(`WaywardBeyond.Client/Voxels/Building/VoxelMaterials.cs:10`) owns the shared
opaque, masked, and blend materials and render state for both world voxels and
the preview. `VoxelEntityBuilder` consumes the same instance.

The preview uses one brick voxel. It seeds the six adjacent cells with lit air
for lighting and does not copy the destination structure's neighbours, so
connected-texture edges, hidden-face culling, and random texture choice are
approximations.

## Voxel debug gizmos

`VoxelDebugGizmoSystem`
(`WaywardBeyond.Client/Systems/VoxelDebugGizmoSystem.cs`) renders bounding box
lines when the debug overlay is active (`DebugSettings.OverlayVisible`) and the
voxel gizmo toggle is on (`DebugSettings.VoxelGizmosVisible`).

F2 toggles the voxel gizmos while the overlay is open. F3 closes the overlay and
clears the voxel gizmo toggle.

The system renders two bounding boxes:
- Cyan lines around the nearest chunk in the nearest voxel object.
- Yellow lines around the max extents of that voxel object based on its existing
  chunks.

The system preallocates line handles and reuses them each frame with zero heap
allocations per tick.

## Region operations

Multi-cell region extraction, mirror and rotation transforms, bulk apply, and
palette codec round-trips live in `VoxelRegionExtractor`,
`VoxelRegionTransform`, `VoxelRegionApplier`, and `VoxelCodec`. See
[region-blueprints](region-blueprints.md).

## Chunk collision caching

`VoxelCollision.AppendChunk` (`WaywardBeyond.Voxels/VoxelCollision.cs:16-75`)
culls interior full blocks and merges exposed full blocks into 3D greedy `Box3`
primitives.

`VoxelObjectBuilder.BuildChunkCollision`
(`WaywardBeyond.Client/Voxels/Building/VoxelObjectBuilder.cs:57-72`) computes
surface collision boxes for one chunk.

`VoxelChunkRegistry.ChunkColliders` caches `ChunkCollisionData`
(`WaywardBeyond.Client/Voxels/ChunkCollisionData.cs:7`) for each resident chunk.

When a chunk becomes dirty, `VoxelEntityBuilder.RebuildDirtyChunks` resamples
collision boxes only for that chunk. `VoxelObjectBuilder.CombineCollisions`
(`WaywardBeyond.Client/Voxels/Building/VoxelObjectBuilder.cs:122-150`) merges
the cached chunk boxes into one `CompoundShape`. The builder skips the collider
update when dirty chunks produce no collision changes.

On the server, `VoxelWorldComponent.ChunkColliders`
(`WaywardBeyond.Server/Components/VoxelWorldComponent.cs:20`) caches
`ChunkColliderData` per chunk. `VoxelColliderBuilder.RebuildDirtyChunks`
(`WaywardBeyond.Server/Systems/VoxelColliderBuilder.cs:46-79`) rebuilds only
the edited chunk and any orthogonal boundary neighbor chunk before combining.

`JoltPhysicsSystem.TryGetJoltShape`
(`Swordfish/Physics/Jolt/JoltPhysicsSystem.cs:206-241`) deduplicates child shapes
using `Shape` value equality (`Swordfish.Library/Types/Shapes/Shape.cs:12-120`).
It creates only one native `BoxShape` per unique size, instead of allocating one
per surface voxel.

## Tests

`WaywardBeyond.Voxels.Tests` pins addressing at chunk boundaries for sizes 8, 16,
and 32. It also pins sampling parity, orientation masking, the snapshot copy, and
palette counts.

`WaywardBeyond.Tests/ChunkBasedVoxelRemeshingTests.cs` pins dirty tracking, boundary
chunk invalidation, single-voxel edit execution budgets, localized light
propagation, border sky light restoration on deletion, collision caching,
debug bounding box gizmo tracking, and prediction lighting equality.

`WaywardBeyond.Tests/BrickPreviewTests.cs` pins the preview mesh for every shape,
the centered block geometry, lit vertex colors, texture layer selection, alpha
mode geometry and material selection, the orientation rule for orientable and
non-orientable bricks, and the preview entity show, hide, deferred replacement,
pause, and release lifecycle.

`WaywardBeyond.Tests/MeshOutlineTests.cs` pins the feature-edge extraction,
depth-tested line use, outset, visibility toggle, and open-mesh boundary edges.
