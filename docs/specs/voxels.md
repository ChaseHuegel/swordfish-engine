# Voxels: The Chunked Container, Coordinates, and Sampling

One subject: how a structure stores voxels in memory and how a reader samples
them. Brick identity and the persisted palette live in
[brick-identity](brick-identity.md).

## The container

`VoxelObject` (`WaywardBeyond.Voxels/VoxelObject.cs`) is a sparse, chunked
container. It owns a `Dictionary<Short3, ChunkData>` keyed by chunk offset. It
creates a chunk on the first write that lands in it.

- `Set(x, y, z, voxel)` writes one cell and creates the chunk when absent
  (`VoxelObject.cs:83`).
- `Get(x, y, z)` returns one cell by value (`VoxelObject.cs:121`).
- `Sample(x, y, z)` returns the cell and its six neighbours
  (`VoxelObject.cs:161`).
- `GetChunkInfos()` returns a copy of every resident chunk. The caller owns the
  voxel arrays (`VoxelObject.cs:263`).

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

## Tests

`WaywardBeyond.Voxels.Tests` pins addressing at chunk boundaries for sizes 8, 16,
and 32. It also pins sampling parity, orientation masking, the snapshot copy, and
palette counts.
