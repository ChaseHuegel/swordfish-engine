# Brick Identity: Namespaced Ids, Transient Voxel Ids, and the Palette

One subject ties voxel content together: how a brick is named and how that name
becomes a voxel id, on disk, on the wire, and at runtime.

## Canonical identity

The canonical identity of a brick (and of the items that place it) is a
**namespaced string id**. Base-game ids are prefixed with the `wb` namespace,
for example `wb:rock`. A mod declares its own namespace, so no mod brick can
shadow a base brick by name.

- Namespacing lives in `WaywardBeyond.Shared.Data/Bricks/BaseBrickCatalog.cs`
  (`Namespace`, `Namespaced`).
- `BrickDatabase` loads the brick `ID` from each `assets/bricks/*.toml` row and
  keys everything by that namespaced string.
- Skill tag sets, the starter inventory, loot, and the interaction/place and
  break resolution all use the same namespaced string ids.

The `wb` prefix is purely a string convention. It does not change voxel id
values. It makes name collisions between content authors impossible.

## Runtime voxel id

A voxel packs a `ushort ID` (`WaywardBeyond.Shared.Data/CodeGen/voxels.nsd:39`).
That id comes from a sorted, collision-free registry. `BrickIdRegistry`
(`WaywardBeyond.Shared.Data/Bricks/BrickIdRegistry.cs`) assigns ids by sorting
the present brick names, so id 0 (the empty voxel) is reserved and ids depend
only on the name set, never on load order. `BrickDatabase`
(`WaywardBeyond.Client.Core/Bricks/BrickDatabase.cs`) builds the id space over
`BaseBrickCatalog.Registry` plus its loaded extras and owns it as the process
`IBrickIdMap`.

`IBrickIdMap` (`WaywardBeyond.Shared.Data/Bricks/IBrickIdMap.cs`) is the single
name-to-id lens every consumer resolves through: `Id(name)` (0 for an unknown
name), `Name(id)`, `Count`. It is injected, never a global static.

| Consumer | Resolves through |
|---|---|
| `SkillDatabase` | an injected `IBrickIdMap` |
| `WorldGenerator` / `WorldMaterialCatalog` | an `IBrickIdMap` constructor argument |
| `PlaceableBrick.ToVoxel` / `SharedInteractionResolver` | the caller's `IBrickIdMap` |
| `VoxelEntityDataCodec` | an `IBrickIdMap` argument |
| `ClientJoinSystem`, `ClientVoxelReconcileSystem` | an injected `IBrickIdMap` |

## Persistent voxel data

A saved world structure is a `VoxelEntityData`, which since **data version 4**
carries a brick palette: `BrickPalette[n]` is the brick name for voxel id `n`
(`WaywardBeyond.Shared.Data/CodeGen/voxels.nsd:22`). A live structure already
carries registry ids; `VoxelEntityDataCodec.EncodeToPalette` records a name for
each present id. The palette is the durable, self-describing identity. It
survives content changes, because a later run resolves the palette name through
its own `IBrickIdMap`.

`VoxelEntityDataCodec` (`WaywardBeyond.Shared.Gameplay/Saves/VoxelEntityDataCodec.cs`)
owns the boundary:

- `EncodeToPalette` attaches a palette to live registry-id data.
- `EncodeLegacyToPalette` re-indexes legacy bare-name FNV ids (version 3 saves).
- `DecodeToLocal` resolves a palette name to the caller's local id space.

`WorldSaveService` encodes palettes when it writes and decodes when it loads.
On join the server attaches a palette to each streamed structure, and the client
decodes it in `ClientJoinSystem` before building its view world.

## Network reconciliation

Voxel edits reconcile by **canonical name**, not by numeric id. The server
broadcasts each edit in a `VoxelEditMessage` carrying `BrickId` (the brick name,
`network.nsd`). `ClientVoxelReconcileSystem` compares the predicted id and the
authoritative name through its local `IBrickIdMap`, so a client and server with
different registries still agree, and a snapped edit is written with the local
id for that name. Placement authors the placed voxel via the caller's map too,
so each side uses its own id space.

## Data format versioning and migration

Every save-bearing record stamps a data version. The current version is
`SaveVersion.CurrentDataVersion` (`WaywardBeyond.Shared.Data/SaveVersion.cs`),
now `4`. The v3→v4 migration, `VoxelEntityDataV3ToV4Migration`
(`WaywardBeyond.Shared.Gameplay/Saves/`), re-indexes a legacy bare-name FNV
palette-less structure into a v4 palette. `SaveMigrator`
(`WaywardBeyond.Shared.Data/Saves/SaveMigrator.cs`) gates on the version and
refuses records stamped by a newer build. See [persistence](persistence.md).

## Tests that pin this

- `Swordfish.Tests/BrickIdRegistryTests.cs` — id ordering, determinism, round-trip.
- `Swordfish.Tests/VoxelEntityDataCodecTests.cs` — palette encode/decode and the
  legacy bare-name reverse map.
- `Swordfish.Tests/SaveMigratorTests.cs` — migration ordering, gate, and refusal.
- `Swordfish.Tests/SharedWorldGenTests.cs` — worldgen ids follow the registry rule.
- `WaywardBeyond.Client.Core.Tests/ClientVoxelReconcileSystemTests.cs` —
  reconcile by brick name across differing registries.