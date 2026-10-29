# Voxels: The Chunked Container, Coordinates, and Sampling

One subject: how a structure stores voxels in memory and how a reader samples
them. Brick identity and the persisted palette live in
[brick-identity](brick-identity.md).

## The container

`VoxelObject` (`WaywardBeyond.Voxels/VoxelObject.cs`) is a sparse, chunked
container. It owns a `Dictionary<Short3, ChunkData>` keyed by chunk offset. It
creates a chunk on the first write that lands in it.

- `Set(x, y, z, voxel)` writes one cell and creates the chunk when absent
  (`VoxelObject.cs:92`).
- `SetLight(x, y, z, lightLevel)` updates the light level of a resident cell
  without modifying palette counts (`VoxelObject.cs:131`).
- `Get(x, y, z)` returns one cell by value (`VoxelObject.cs:168`).
- `Sample(x, y, z)` returns the cell and its six neighbours
  (`VoxelObject.cs:208`).
- `GetChunkInfos()` returns a copy of every resident chunk. The caller owns the
  voxel arrays (`VoxelObject.cs:310`).

A chunk holds `chunkSize³` voxels. `chunkSize` is a power of two and the
constructor rejects any chunk whose `Size`, voxel count, or array does not match
(`VoxelObject.cs:45`).

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

`chunkShift` is `log2(chunkSize)` and `chunkShift2` is twice that. A negative
world coordinate shifts forward by its chunk span before the mask applies, so
`-1` lands on the last cell of chunk `-1`.

### Out of bounds

A read outside a resident chunk returns the shared empty voxel. That voxel
carries light level 15, so an absent chunk reads as lit sky
(`VoxelObject.cs:14`).

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
(`ChunkData.cs:112`), so the mesh and collider passes follow a rotated brick.
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

A voxel orientation is a byte. Only the low six bits are valid, and the
orientation lookup masks the rest (`ChunkData.cs:147`).

## Palette maintenance

Each chunk carries a `VoxelPalette` (`WaywardBeyond.Voxels/VoxelPalette.cs`), a
count of each voxel id. The pass pipeline uses the counts to skip a chunk that
cannot contribute (`VoxelPalette.cs:50`). `VoxelExtensions.Set` updates the count
on every write.

Id 0 is the empty voxel. `Only(0)` is true for a chunk that holds no content.

## Chunk remeshing and dirty tracking

Client structures mesh each resident chunk into child entities
(`WaywardBeyond.Client/Voxels/Building/VoxelEntityBuilder.cs:75`). Each chunk
child entity holds its own `MeshRendererComponent` and `VoxelChunkComponent`
(`WaywardBeyond.Client/Components/VoxelChunkComponent.cs`).

`VoxelChunkRegistry` (`WaywardBeyond.Client/Voxels/VoxelChunkRegistry.cs`)
tracks dirty chunk coordinates, active chunk child entities, and cached chunk
collision data.

A voxel edit marks its resident chunk dirty. An edit on a chunk boundary also
marks the orthogonal neighbor chunk dirty.

`VoxelLightingService`
(`WaywardBeyond.Client/Voxels/Processing/VoxelLightingService.cs:30-257`) runs
dual-queue BFS flood-fill lighting across resident chunks. The service marks
every modified cell and its chunk dirty.

`DepthState`
(`WaywardBeyond.Client/Voxels/Processing/DepthState.cs:12-295`) tracks min and max
depth bounds across XY, XZ, and ZY projection planes in `VoxelChunkRegistry`.
An air cell is an ambient light source (`DepthState.IsAmbientSource`) when its
coordinates lie immediately outside the solid boundary (`depth.Max + 1` or
`depth.Min - 1`) along at least one axis and the cell is not enclosed on both
perpendicular projection axes. Interior air cells inside cavities and tunnels lie
between orthogonal depth bounds and return false.

`DepthPrePass`
(`WaywardBeyond.Client/Voxels/Processing/DepthPrePass.cs:6-28`) populates
`DepthState` during initial voxel entity construction. The pass filters out
transparent and non-culling bricks so they do not expand depth bounds.

`AmbientLightPass`
(`WaywardBeyond.Client/Voxels/Processing/AmbientLightPass.cs:8-61`) seeds ambient
light level 15 at resident min and max voxels on each depth plane and enqueues
them for propagation.

When a solid culler voxel is placed (`VoxelLightingService.cs:64-151`), occluded
light drains through `_removeQueue`. If the placed block replaces open-sky space,
the initial drain light level is 15. During drain removal
(`VoxelLightingService.cs:197-281`), brighter neighbors and equal-decay neighbors
(`< 15`) re-propagate light via `_addQueue`. At maximum ambient sky light (level 15),
equal-light neighbors drain unless they are independent sky sources (lamps, exterior
ambient sources, or cells adjacent to non-resident space). This keeps lit tunnel
neighbors illuminated while draining enclosed spaces when sealed across chunk borders.
The service updates depth bounds via `DepthState.Add` and checks adjacent air cells:
any air cell that is an exterior ambient source (`DepthState.IsAmbientSource`) receives
ambient light level 15 and propagates light outward through `_addQueue`. Interior
air cells inside cavities and tunnels are not ambient sources and receive no
ambient sky light.

When a solid culler voxel is deleted (`VoxelLightingService.cs:97-183`), the
service updates depth bounds via `DepthState.Remove`. The cell receives decayed
light from the brightest resident neighbor (`maxNeighborLight - 1`), or light
level 15 if adjacent to non-resident space. Light entering a cavity or tunnel
decays with distance. When the entrance is blocked off, the decayed light drains
to zero through `_removeQueue`, leaving enclosed interiors dark.

`VoxelEntityBuilder.RebuildDirtyChunks`
(`WaywardBeyond.Client/Voxels/Building/VoxelEntityBuilder.cs:153-220`) rebuilds
only dirty chunks. Untouched chunks keep their existing mesh renderers.

`ClientVoxelReconcileSystem`
(`WaywardBeyond.Client/Systems/ClientVoxelReconcileSystem.cs:232-265`) reconciles
predicted edits against authoritative server echoes. `VoxelEquals` compares brick
identity, shape, and orientation. It does not compare light level because server
echoes do not compute presentation lighting. This avoids false misprediction
snaps that would clear locally calculated air lighting to zero. `WriteVoxel` also
preserves existing air light when writing an authoritative empty cell.

## Voxel debug gizmos

`VoxelDebugGizmoSystem`
(`WaywardBeyond.Client/Systems/VoxelDebugGizmoSystem.cs:17-175`) renders bounding
box lines when the debug overlay is active (`DebugSettings.OverlayVisible`).

The system renders two bounding boxes:
- Cyan lines around the nearest chunk in the nearest voxel object.
- Yellow lines around the max extents of that voxel object based on its existing
  chunks.

The system preallocates line handles and reuses them each frame with zero heap
allocations per tick.

## Chunk collision caching

`VoxelObjectBuilder.BuildChunkCollision`
(`WaywardBeyond.Client/Voxels/Building/VoxelObjectBuilder.cs:60-77`) computes
surface collision boxes for one chunk.

`VoxelChunkRegistry.ChunkColliders` caches `ChunkCollisionData`
(`WaywardBeyond.Client/Voxels/ChunkCollisionData.cs:10`) for each resident chunk.

When a chunk becomes dirty, `VoxelEntityBuilder.RebuildDirtyChunks` resamples
collision boxes only for that chunk. `VoxelObjectBuilder.CombineCollisions`
(`WaywardBeyond.Client/Voxels/Building/VoxelObjectBuilder.cs:80-111`) merges
the cached chunk boxes into one `CompoundShape`. The builder skips the collider
update when dirty chunks produce no collision changes.

`JoltPhysicsSystem.TryGetJoltShape`
(`Swordfish/Physics/Jolt/JoltPhysicsSystem.cs:204-242`) deduplicates child shapes
using `Shape` value equality (`Swordfish.Library/Types/Shapes/Shape.cs:12-85`).
It creates only one native `BoxShape` per unique size, instead of allocating one
per surface voxel.

## Tests

`WaywardBeyond.Voxels.Tests` pins addressing at chunk boundaries for sizes 8, 16,
and 32. It also pins sampling parity, orientation masking, the snapshot copy, and
palette counts.

`Swordfish.Tests/ChunkBasedVoxelRemeshingTests.cs` pins dirty tracking, boundary
chunk invalidation, single-voxel edit execution budgets, localized light
propagation, border sky light restoration on deletion, collision caching,
debug bounding box gizmo tracking, and prediction lighting equality.
