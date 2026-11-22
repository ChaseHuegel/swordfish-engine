# Brick Identity: Namespaced Ids, Transient Voxel Ids, and the Palette

One subject ties voxel content together: how a brick is named and how that name
becomes a voxel id, on disk, on the wire, and at runtime.

## Canonical identity

The canonical identity of a brick (and of the items that place it) is a
**namespaced string id**. Base-game ids are prefixed with the `wb` namespace,
for example `wb:rock`. A mod declares its own namespace, so no mod brick can
shadow a base brick by name.

- Namespacing is a `wb:` string prefix on `Brick.ID`
  (`WaywardBeyond.Bricks/Brick.cs`).
- `BrickDatabase` loads the brick `ID` from each `assets/bricks/*.toml` row and
  keys everything by that namespaced string.
- Skill tag sets, the starter inventory, loot, and the interaction/place and
  break resolution all use the same namespaced string ids.

The `wb` prefix is purely a string convention. It does not change voxel id
values. It makes name collisions between content authors impossible.

Bricks are defined only in toml. Nothing hardcodes the brick set in the runtime
API. One content seed references known base names in code: the starter loadout
in `PlayerInitialInventory` (`WaywardBeyond.Server/Systems/`). The worldgen
material names ship as `levelgen.toml` defaults (`Asteroid.Materials`).

## Runtime voxel id

A voxel packs a `ushort ID` (`WaywardBeyond.Data/Nsd/voxels.nsd:51`).
That id comes from a sorted, collision-free registry. `BrickRegistry`
(`WaywardBeyond.Bricks/BrickRegistry.cs`) assigns ids by sorting
the present brick names, so id 0 (the empty voxel) is reserved and ids depend
only on the name set, never on load order. `BrickDatabase`
(`WaywardBeyond.Bricks/BrickDatabase.cs`) builds the id space purely from
its loaded toml content and owns it as the process `IBrickRegistry`. It parses
each toml once through the base `VirtualAssetDatabase`, stages the definitions,
then builds the sorted registry with `BrickRegistry.FromNames` and binds each
brick's id. Name lookups use the base store; id lookups index a lock-free array,
so the per-voxel path takes no lock. A brick's id shifts only when the present
content set changes, which is safe because edits reconcile by name and saved
structures carry a palette.

`IBrickRegistry` (`WaywardBeyond.Bricks/IBrickRegistry.cs:9`) is the single
name-to-id lens every consumer resolves through. It is injected, never a global
static. It separates the id space into two ranges:

- **Registered space (`1 .. Count`)**: Loaded content occupies this public space.
  `Id(name)` queries only registered content and returns `0` for an unregistered
  name. Content validation in `SkillDatabase`, `AsteroidGenerator`, and
  `InteractionResolver` uses `Id(name)` to reject unknown bricks.
- **Extension space (`Count + 1 .. ushort.MaxValue`)**: `Resolve(name)`
  (`WaywardBeyond.Bricks/BrickRegistry.cs:58`) returns the registered id when
  present, or allocates a process-stable extension id for an unknown non-empty
  name. Extension allocations do not change `Count`, `Ids`, or `BrickDatabase`.
- **Reverse lookup**: `Name(id)` (`WaywardBeyond.Bricks/BrickRegistry.cs:91`)
  resolves both registered ids and allocated extension ids back to their
  canonical string names.

| Consumer | Resolves through |
|---|---|
| `SkillDatabase` | `IBrickRegistry.Id` |
| `LevelGenerator` / `AsteroidGenerator` | `IBrickRegistry.Id` |
| `BrickExtensions.ToVoxel` / `InteractionResolver` | `IBrickRegistry.Id` |
| `VoxelCodec.Decode` | `IBrickRegistry.Resolve` |
| `VoxelCodec.Encode` | `IBrickRegistry.Name` |
| `ClientJoinSystem` | `VoxelCodec.Decode` via an injected `IBrickRegistry` |
| `ClientVoxelReconcileSystem` | `IBrickRegistry.Resolve` and `IBrickRegistry.Name` |

`Brick` (`WaywardBeyond.Bricks/Brick.cs`) is the headless view of
a loaded brick. It carries the voxel id, shape, textures, tags, and the custom
mesh **id** (a string). It holds no renderable mesh and builds no voxel, so the
server can reuse it. The client resolves the mesh id to a renderable mesh
through an `IAssetDatabase<Mesh>` in the mesh-build, placement-preview, and gizmo
paths. Brick tags are case-insensitive.

### Missing-brick placeholder rendering

An extension id has no `Brick` entry in `BrickDatabase`. When `MeshPostPass`
(`WaywardBeyond.Client/Voxels/Processing/MeshPostPass.cs:51`) samples a non-zero
voxel id that `BrickDatabase.Get` does not know, it falls back to a static
opaque placeholder `Brick` (`MeshPostPass.cs:24`) with empty textures. Both cube
and custom-shape mesh builders map a missing texture name to layer `0` of the
brick `TextureArray`. `PBRTextureArraysParser`
(`WaywardBeyond.Client/Graphics/PBRTextureArraysParser.cs:83`) reserves layer
`0` for a generated magenta-and-black checkerboard texture, so unresolved bricks
render in place with their saved shape and orientation instead of disappearing.

## Persistent voxel data

A saved world structure is a `VoxelEntityData`, which since **data version 4**
carries a brick palette. `BrickPalette[n]` is the brick name for voxel id `n`
(`WaywardBeyond.Data/Nsd/voxels.nsd:22`). A live structure already
carries registry or extension ids. `VoxelCodec.Encode` records a name for
each present id. The palette is the durable, self-describing identity. It
survives content changes, because a later run resolves the palette name through
its own `IBrickRegistry`.

`VoxelCodec` (`WaywardBeyond.Voxels/VoxelCodec.cs:8`) owns the boundary:

- `Encode` (`VoxelCodec.cs:11`) attaches a palette to live voxel data through
  `IBrickRegistry.Name`. Both registered and extension ids write their original
  brick names back into the palette.
- `Decode` (`VoxelCodec.cs:37`) resolves each non-empty palette name to the
  caller's local id space through `IBrickRegistry.Resolve`. Unknown brick names
  receive extension ids and survive a save round trip without loss.

`LevelSaveService` encodes palettes when it writes and decodes when it loads.
On join the server attaches a palette to each streamed structure, and the client
decodes it in `ClientJoinSystem` before building its view world.

## Network reconciliation

Voxel edits reconcile by **canonical name**, not by numeric id. The server
broadcasts each edit in a `VoxelEditMessage` carrying `BrickId` (the brick name,
`network.nsd`). `ClientVoxelReconcileSystem`
(`WaywardBeyond.Client/Systems/ClientVoxelReconcileSystem.cs:100`) compares the
predicted id and the authoritative name through its local `IBrickRegistry`. A
client and server with different registries still agree, and a snapped or
unpredicted edit resolves `BrickId` via `IBrickRegistry.Resolve`. Placement
authors the placed voxel via the caller's map too, so each side uses its own id
space.

## Data format versioning and migration

Every save-bearing record stamps a data version. The current version is
`DataVersion.CURRENT` (`WaywardBeyond.Data/DataVersion.cs`), now `4`.
The palette is the durable identity, and pre-palette (data version 3) saves are
unsupported. `Migrator` (`WaywardBeyond.Data/Migrations/Migrator.cs`)
and `IMigration` remain for future record migrations, but no migrations ship in
this build. `DataComposition` registers an empty `Migrator`. See
[persistence](persistence.md).

## Paint identity and palette

Paint assets follow the same identity model as bricks. `IPaintRegistry`
(`WaywardBeyond.Bricks/IPaintRegistry.cs`) and `PaintRegistry`
(`WaywardBeyond.Bricks/PaintRegistry.cs`) assign one unified sorted `ushort` id
space across texture paints and color swatches (`0` means no override,
`1 .. Count` are registered paints, `Count + 1 .. ushort.MaxValue` are
extension ids allocated by `Resolve`).

`VoxelEntityData.PaintPalette` (`WaywardBeyond.Data/Nsd/voxels.nsd:23`) records
canonical paint names for every `TexturePaintId` and `ColorPaintId` present in
chunk `FaceOverrides`. `VoxelCodec.Encode` and `VoxelCodec.Decode`
(`WaywardBeyond.Voxels/VoxelCodec.cs`) translate paint ids through
`IPaintRegistry`, so unknown modded paints survive save and network round trips.
Unresolved texture paint extension ids render with layer `0` (the missing
checkerboard texture) and `Connected = false`. Unresolved color paint extension
ids render untinted.

## Tests that pin this

- `WaywardBeyond.Voxels.Tests/VoxelPaletteTests.cs:86` (`VoxelCodecTests`) pins
  palette encode, palette decode, extension id allocation in `BrickRegistry`,
  and lossless unknown-brick round trips across modded and vanilla registries.
- `WaywardBeyond.Voxels.Tests/FaceOverrideTests.cs:8` (`FaceOverrideTests`) pins
  `PaintPalette` encode/decode, unknown-paint round trips, and two-stage decode
  preservation.
- No tests pin the migration path because no migrations ship in this build.