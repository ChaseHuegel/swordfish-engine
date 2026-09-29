# Brick Identity: Namespaced Ids, Transient Voxel Ids, and the Palette

One subject ties voxel content together: how a brick is named and how that name
becomes a voxel id, on disk, on the wire, and at runtime.

## Canonical identity

The canonical identity of a brick (and of the items that place it) is a
**namespaced string id**. Base-game ids are prefixed with the `wb` namespace,
for example `wb:rock`. A mod declares its own namespace, so no mod brick can
shadow a base brick by name.

- Namespacing lives in `WaywardBeyond.Shared.Gameplay/Bricks/BaseBrickCatalog.cs`
  (`Namespace`, `Namespaced`).
- `BrickDatabase` loads the brick `ID` from each `assets/bricks/*.toml` row and
  keys everything by that namespaced string.
- Skill tag sets, the starter inventory, loot, and the interaction/place and
  break resolution all use the same namespaced string ids.

The `wb` prefix is purely a string convention. It does not change voxel id
values. It makes name collisions between content authors impossible.

## Runtime voxel id

A voxel packs a `ushort ID` (`WaywardBeyond.Shared.Data/CodeGen/voxels.nsd:39`).
That id is the **FNV1a hash** of the brick name, derived by
`FNV1a.ComputeDataID` (`WaywardBeyond.Shared.Data/FNV1a.cs:32`). It is a pure,
deterministic function of the name. The client, the server, worldgen, and the
skills database derive the same id from the same string. Id 0 is reserved for
the empty voxel (air).

`BrickDatabase.GenerateDataID` (`WaywardBeyond.Client.Core/Bricks/BrickDatabase.cs`)
uses only that pure hash. It never shifts an id by insertion order, so brick ids
are stable across load orders and runs. A genuine FNV collision between two brick
names is a hard load error, never a silent remap. This is what prevents a placed
brick from being re-read as a different brick after a reload.

`BaseBrickCatalog.Registry` (`WaywardBeyond.Shared.Gameplay/Bricks/BrickIdRegistry.cs`)
also provides a collision-free **sequential** id space over the base names.
Worldgen and persistence use it to normalize the sparse FNV space into a compact
palette. See [persistence](persistence.md).

## Persistent voxel data

A saved world structure is a `VoxelEntityData`, which since **data version 4**
carries a brick palette: `BrickPalette[n]` is the brick name for voxel id `n`
(`WaywardBeyond.Shared.Data/CodeGen/voxels.nsd:22`). Saving re-indexes each voxel
from its FNV id to a compact registry id and records the name in the palette. The
palette is the durable, self-describing identity. It survives content changes,
because a later run resolves the palette name through its own registry.

`VoxelEntityDataCodec` (`WaywardBeyond.Shared.Gameplay/Saves/VoxelEntityDataCodec.cs`)
owns this boundary:

- `EncodeToPalette` re-indexes live FNV ids (current build) into a palette.
- `EncodeLegacyToPalette` re-indexes legacy bare-name FNV ids (version 3 saves).
- `DecodeToLocal` resolves a palette back to a caller's local id space.

`WorldSaveService` (`WaywardBeyond.Server.Core/Saves/WorldSaveService.cs`) encodes
palettes when it writes a world and decodes them when it loads one.

The live join and edit paths still carry legacy FNV ids. The palette is applied
on the persistence boundary only. A later milestone will extend it to the join
stream and reconcile path.

## Data format versioning and migration

Every save-bearing record stamps a data version. The current version is
`SaveVersion.CurrentDataVersion` (`WaywardBeyond.Shared.Data/SaveVersion.cs:15`),
now `4`. `WaywardBeyond.Version` and `WorldSaveService._gameVersion` both derive
from it.

`SaveMigrator` (`WaywardBeyond.Shared.Data/Saves/SaveMigrator.cs`) applies
forward migrations keyed by record type and by data version. Rules:

- A record at the current version passes through unchanged.
- A record at an older version runs each `ISaveMigration` step up to current.
- A record stamped by a **newer** build is refused with
  `SaveDataNotSupportedException`.
- A type with no registered step for a version is left unchanged (its format did
  not change in that version).

The v3→v4 migration is `VoxelEntityDataV3ToV4Migration`
(`WaywardBeyond.Shared.Gameplay/Saves/VoxelEntityDataV3ToV4Migration.cs`). It
re-indexes a legacy bare-name FNV palette-less structure into a v4 palette.

`WorldSaveService.LoadLevel` runs the level's version gate and migrates each
loaded structure. `NatsCharacterStorage` gates and migrates characters.
`CharacterSaveMigrations` (`WaywardBeyond.Shared.Data/Saves/CharacterSaveMigrations.cs`)
currently registers no step; the character record is unchanged in v4.

## Tests that pin this

- `Swordfish.Tests/BrickIdRegistryTests.cs` — id ordering, determinism, round-trip.
- `Swordfish.Tests/VoxelEntityDataCodecTests.cs` — palette encode/decode and the
  legacy bare-name reverse map.
- `Swordfish.Tests/SaveMigratorTests.cs` — migration ordering, gate, and refusal.
- `Swordfish.Tests/SharedWorldGenTests.cs` — worldgen ids follow the catalog rule.