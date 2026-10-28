# Asset Definition TOML Formats

One subject: the TOML config types that define game content (items, bricks,
materials, skills). Item definitions live under `WaywardBeyond.Items/assets/`.
Brick definitions live in `WaywardBeyond.Bricks/assets/`. Skill definitions live
in `WaywardBeyond.Skills/assets/`. Brick and item localization stays
client-owned.

Parser registration: `WaywardBeyond.Items/Injector.cs:15`
(`RegisterTomlParser<ItemDefinitions>`), `WaywardBeyond.Bricks/Injector.cs:14`
(`RegisterTomlParser<BrickDefinitions>`), and
`WaywardBeyond.Skills/Injector.cs:16` (`RegisterTomlParser<SkillDefinitions>`).

## Items (`assets/items/*.toml`)

Schema: `WaywardBeyond.Items/ItemDefinitions.cs` (collection),
`ItemDefinition.cs` (row). The module loads them headlessly, so the dedicated
server resolves a placeable without the client. Render assets stay client-side:
the item carries the icon and model ids, and the client resolves them.

Each row is a `[[Items]]` table followed by optional sub-tables:

| Field | Type | Schema class |
|---|---|---|
| `ID` | string | `ItemDefinition.ID` |
| `Name` | string (localization key) | `ItemDefinition.Name` |
| `Icon` | string? | `ItemDefinition.Icon` |
| `MaxStack` | int? | `ItemDefinition.MaxStack` |
| `[Items.Placeable]` | sub-table | `PlaceableDefinition` (`Type`, `ID`) |
| `[Items.Tool]` | sub-table | `ToolDefinition` (`Type`, `Target`, `Tags`) |
| `[Items.ViewModel]` | sub-table | `ModelDefinition` |
| `[Items.WorldModel]` | sub-table | `ModelDefinition` |

### Placeable

`PlaceableDefinition` (`PlaceableDefinition.cs`): `Type` (`PlaceableType`),
`ID` (the namespaced brick ID it places).

Example:

```toml
[Items.Placeable]
Type = "brick"
ID = "wb:core"
```

### Tool

`ToolDefinition` (`ToolDefinition.cs`): `Type` (`ToolType`), `Target`, `Tags[]`.

### Model

`ModelDefinition` (`ModelDefinition.cs`): `Mesh`, `Material`, `Position`/
`Rotation`/`Scale` (each a `Float3` with `X`/`Y`/`Z`). `Scale` defaults to
`1,1,1`.

Example:

```toml
[Items.ViewModel]
Mesh       = "cube.obj"
Material   = "bricks/core"
Position.X = 0.4
Scale.X    = 0.25
```

## Bricks (`assets/bricks/*.toml`)

Schema: `WaywardBeyond.Bricks/BrickDefinitions.cs` (collection),
`BrickDefinition.cs` (row). The brick tomls live in the `WaywardBeyond.Bricks`
module (mirroring the skills module), so the block sides author and the headless
server share one database.

Each row is a `[[Bricks]]` table with an optional `[Bricks.Textures]` sub-table:

| Field | Type | Notes |
|---|---|---|
| `ID` | string | unique, namespaced brick id (e.g. `wb:rock`) |
| `Alpha` | `BrickAlpha` enum | `Opaque` (default), `Masked`, `Blend` (`WaywardBeyond.Bricks/BrickAlpha.cs`) |
| `Passable` | bool | |
| `Mesh` | string? | |
| `Shape` | `BrickShape` enum | `WaywardBeyond.Bricks/BrickShape.cs` |
| `Textures` | sub-table | `BrickTextures` |
| `Tags` | string[] | |

Base-game brick ids are prefixed with the `wb` namespace. A mod uses its own
namespace so no two content authors can collide by name. A brick's voxel id is
assigned by the sorted registry at database load (ordinal by name, id 0 reserved
for the empty voxel), so ids depend only on the name set, never on load order.
Persisted structures carry a brick palette so a saved id stays self-describing
across content changes; the legacy `FNV1a.ComputeDataID` hash is used only to
reverse-map pre-palette (data version 3) saves. See
[brick-identity](brick-identity.md).

Example:

```toml
[[Bricks]]
ID      = "wb:core"
Shape   = "block"
Tags    = ["metal", "buildable", "convertable_truss"]
[Bricks.Textures]
Default = ["core"]
```

### Textures

`BrickTextures` (`BrickTextures.cs`): `Connected` plus per-face string?[] fields:
`Default`, `Top`, `Bottom`, `Front`, `Back`, `Left`, `Right`. `Default` is the
fallback face; faces override it.

## Materials (`assets/materials/**/*.toml`)

Flat config: `Swordfish/IO/MaterialDefinition.cs`. Keys include `Shader`,
`Textures`, `Transparent`. Example at
`assets/materials/bricks/glass.toml`. These are engine-side material
definitions, not game content tables.

## Skills (`assets/skills/*.toml`)

Skills moved to the shared `WaywardBeyond.Skills` module; the client no
longer owns skill definitions or runs any skill logic. Schema:
`WaywardBeyond.Skills/SkillDefinitions.cs` (collection),
`SkillDefinition.cs` (row), `XPSource.cs` (enum: `Place`, `Break`).

Each row is a `[[Skills]]` table:

| Field | Type | Notes |
|---|---|---|
| `ID` | string | |
| `Name` | string (localization key) | |
| `Category` | string (localization key) | |
| `Icon` | string? | |
| `[Skills.Sources.<Source>]` | map: brick id or `tag:X` → XP | keyed by `XPSource` (Place/Break) |
| `[Skills.Levels]` | map: level → XP to advance past the previous level | a level is reached only when total XP exceeds the running sum of requirements, and it is sorted on load |

Tags expand through the invariant `lang/tags/*.toml` lists into brick data ids
on load. The lists live in `WaywardBeyond.Skills/assets/lang/tags/`. `Name` and
`Category` are localization keys. The server sends `Name` in notifications and
the client resolves it. `Icon` and `Category` have no reader yet (see
[0068](../issues/0068-skill-panel.md)).

A skill is rejected at load when it has no `ID`, when its `ID` is already
declared, when it carries no level curve, or when a level or its requirement is
below 1. A rejected skill grants no XP. See [skills](skills.md).

## Bodies (`assets/bodies/*.toml`)

Bodies live in the shared `WaywardBeyond.Bodies` module. Schema:
`BodyDefinitions.cs` (collection), `BodyDefinition.cs` (row), `Body.cs` (loaded
runtime asset).

Each body is a `[[Bodies]]` table carrying a stable namespaced string ID
(`wb:human_m`). The `States` member is a tag-keyed map of state variants
(`standing`, `floating`, ...); each state maps direction tags (`front`, `back`,
... , free-form for future sets) to texture paths. Direction display order is
canonical, not the asset order: `front` is index 0, then `back`/`left`/`right`
(`BodyDatabase._orderedDirections`, `BodyDatabase.cs:14`). An unknown body ID
falls back to the first loaded body at resolution.

Each body declares a `Species` and a `Gender`. A `Species` groups its body
variants, so `human` groups `wb:human_m` and `wb:human_f`. The database lists
the species in natural order (`BodyDatabase.Species`, `BodyDatabase.cs:40`),
reports the genders a species declares (`BodyDatabase.GetGenders`,
`BodyDatabase.cs:43`), and resolves a species and gender pair to one body
(`BodyDatabase.GetBody`, `BodyDatabase.cs:49`). A gender the species does not
declare falls back to the first variant of the species. An unknown species
resolves to no body, so the client falls back to the default body ID. A body
without a `Species` uses its `ID` as the species. A body without a `Gender`
loads as `Neutral`.

The current set is `wb:human_m`, `wb:human_f`, `wb:bot` (industrial drone),
`wb:bot2_m` / `wb:bot2_f` (anthropoid android), `wb:bot3` (chassis-bound
amoeboid), and the alien lineages `wb:alien_m` / `wb:alien_f`, plus
`wb:alien2_{m,f}` (purple, cylinder head), `wb:alien3_{m,f}` (grey), and
`wb:alien4_{m,f}` (orange, cyclopean). Each gender carries its own ID so name
themes and preview sprites key cleanly. Neutral species such as `bot` and
`bot3` carry a single ID.

Character-name generation keys its localized tags and formats by the same body
ID. `NameGenerator.Generate(key)` resolves `names.{title,first,last,subtitle,nickname}.{key}`
and `formats.name.{key}` (see `NameGenerator.cs:30`). The client passes the
current body ID, so a character named against a body reuses those strings.
Body-named tag lists live in `WaywardBeyond.Client/assets/lang/en/tags/`
(`names_*.en.csv`); the shared `names_save.en.csv` uses the fixed `save` key.

| Field | Type | Notes |
|---|---|---|
| `ID` | string | namespaced body asset ID (e.g. `wb:human_m`) |
| `Species` | string | species grouping of the body (e.g. `human`, `bot`) |
| `Gender` | `Gender` | gender categorization (`Neutral`, `Male`, `Female`) |
| `[Bodies.States.<state>]` | map: direction tag → texture path list | state tag free-form (`standing`, `floating`, ...) |

## Localization

`assets/lang/en/*.toml` and `assets/lang/tags/*.toml` hold localization entries.

Tag list files live under a `tags/` directory segment; other lang files are
localization tables and are not tags. The tag list is shared and general purpose,
hosted in `WaywardBeyond.Data/Localization/`.

## Source of truth

- `WaywardBeyond.Items/{ItemDefinitions,ItemDefinition,ToolDefinition,PlaceableDefinition,ModelDefinition,ItemDatabase,ItemPlaceables}.cs`
- `WaywardBeyond.Bricks/{BrickDefinitions,BrickDefinition,BrickTextures,BrickAlpha}.cs`
- `WaywardBeyond.Skills/{SkillDefinitions,SkillDefinition,XPSource,Skill}.cs`
- `WaywardBeyond.Data/Localization/LocalizedTagsDefinition.cs`
- `WaywardBeyond.Bodies/{BodyDefinitions,BodyDefinition,Body,Gender,BodyDatabase}.cs`
- `WaywardBeyond.Bricks/BrickShape.cs`
- `Swordfish/IO/MaterialDefinition.cs`
- Parser registration: `WaywardBeyond.Skills/Injector.cs:16`,
  `WaywardBeyond.Client/Injector.cs:276`, `WaywardBeyond.Bricks/Injector.cs:14`

## Tests that pin this

`Swordfish.Tests/SkillDatabaseTests.cs` loads the shipped skill and brick tomls
and pins the schema, the tag expansion, and the level curve. See
[skills](skills.md). `Swordfish.Tests/BodyDatabaseTests.cs` loads the shipped
body definitions and pins the species order, the gender groups, the species
carried on each loaded body, and the species and gender resolution.
