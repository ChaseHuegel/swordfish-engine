# Asset Definition TOML Formats

One subject: the TOML config types that define game content (items, bricks,
materials, skills). Item and brick definitions live under
`WaywardBeyond.Client.Core/assets/`; skills moved to the shared
`WaywardBeyond.Shared.Skills/assets/` module, while brick/item localization stays
client-owned.

Parser registration: `WaywardBeyond.Client.Core/Injector.cs:315-317`
(`RegisterTomlParser<BrickDefinitions/ItemDefinitions>`) and
`WaywardBeyond.Shared.Skills/Injector.cs` (`RegisterTomlParser<SkillDefinitions/>`).

## Items (`assets/items/*.toml`)

Schema: `WaywardBeyond.Client.Core/Items/ItemDefinitions.cs` (collection),
`ItemDefinition.cs` (row).

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

Schema: `WaywardBeyond.Shared.Bricks/BrickDefinitions.cs` (collection),
`BrickDefinition.cs` (row). The brick tomls live in the `WaywardBeyond.Shared.Bricks`
module (mirroring the skills module), so the block sides author and the headless
server share one database.

Each row is a `[[Bricks]]` table with an optional `[Bricks.Textures]` sub-table:

| Field | Type | Notes |
|---|---|---|
| `ID` | string | unique, namespaced brick id (e.g. `wb:rock`) |
| `Transparent` | bool | |
| `Passable` | bool | |
| `Mesh` | string? | |
| `Shape` | `BrickShape` enum | `WaywardBeyond.Shared.Gameplay/Bricks/BrickShape.cs` |
| `Textures` | sub-table | `BrickTextures` |
| `Tags` | string[] | |

Base-game brick ids are prefixed with the `wb` namespace. A mod uses its own
namespace so no two content authors can collide by name. A brick's voxel id is
`FNV1a.ComputeDataID(ID)` — a pure, deterministic hash of the namespaced id. The
client, server, worldgen, and skills all derive the same id from the same string,
and a genuine FNV collision between two brick ids is a hard load error. See
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

Skills moved to the shared `WaywardBeyond.Shared.Skills` module; the client no
longer owns skill definitions or runs any skill logic. Schema:
`WaywardBeyond.Shared.Skills/Skills/SkillDefinitions.cs` (collection),
`SkillDefinition.cs` (row), `XPSource.cs` (enum: `Place`, `Break`).

Each row is a `[[Skills]]` table:

| Field | Type | Notes |
|---|---|---|
| `ID` | string | |
| `Name` | string (localization key) | |
| `Category` | string (localization key) | |
| `Icon` | string? | |
| `[Skills.Sources.<Source>]` | map: brick id or `tag:X` → XP | keyed by `XPSource` (Place/Break) |
| `[Skills.Levels]` | map: level → cumulative XP | |

Tags expand through the invariant `lang/tags/*.toml` lists into brick data ids
on load. `Name` and `Category` are localization keys resolved client-side (see
[skills](skills.md) for the authoritative model).

## Bodies (`assets/bodies/*.toml`)

Bodies live in the shared `WaywardBeyond.Shared.Bodies` module. Schema:
`BodyModels.cs` (collection), `BodyModel.cs` (row).

Each body is a `[[Bodies]]` table carrying a stable namespaced string ID
(`wb:m_human`). The `States` member is a tag-keyed map of state variants
(`standing`, `floating`, ...); each state maps direction tags (`front`, `back`,
... , free-form for future sets) to texture paths. Direction display order is
canonical, not the asset order: `front` is index 0, then `back`/`left`/`right`
(`BodyDirectionOrder.cs`). An unknown body ID falls back to the first loaded
body at resolution.

| Field | Type | Notes |
|---|---|---|
| `ID` | string | namespaced body asset ID (e.g. `wb:m_human`) |
| `[Bodies.States.<state>]` | map: direction tag → texture path list | state tag free-form (`standing`, `floating`, ...) |

## Localization

`assets/lang/en/*.toml` and `assets/lang/tags/*.toml` hold localization entries.
Schema classes: `Meta/LocalizedTagsDefinition.cs`,
`Serialization/LocalizedTagDefinitionParser.cs`.

## Source of truth

- `WaywardBeyond.Client.Core/Items/{ItemDefinitions,ItemDefinition,ToolDefinition,PlaceableDefinition,ModelDefinition}.cs`
- `WaywardBeyond.Client.Core/Bricks/{BrickDefinitions,BrickDefinition,BrickTextures}.cs`
- `WaywardBeyond.Shared.Skills/Skills/{SkillDefinitions,SkillDefinition,XPSource}.cs`
- `WaywardBeyond.Shared.Bodies/{BodyModels,BodyModel,BodyDirectionOrder,BodyDatabase}.cs`
- `WaywardBeyond.Shared.Gameplay/Bricks/BrickShape.cs`
- `Swordfish/IO/MaterialDefinition.cs`
- Parser registration: `WaywardBeyond.Shared.Skills/Injector.cs`,
  `WaywardBeyond.Client.Core/Injector.cs`

## Tests that pin this

- `WaywardBeyond.Client.Core.Tests` validate brick/item/skill definition
  parsing from the asset TOML.