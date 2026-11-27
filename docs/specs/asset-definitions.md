# Asset Definition TOML Formats

One subject: the TOML config types that define game content (items, bricks,
materials, skills). Item definitions live under `WaywardBeyond.Items/assets/`.
Brick definitions live in `WaywardBeyond.Bricks/assets/`. Skill definitions live
in `WaywardBeyond.Skills/assets/`. Brick and item localization stays
client-owned.

Parser registration: `WaywardBeyond.Items/Injector.cs:15`
(`RegisterTomlParser<ItemDefinitions>`), `WaywardBeyond.Bricks/Injector.cs:14`
(`RegisterTomlParser<BrickDefinitions>`), and `WaywardBeyond.Skills/Injector.cs:16`
(`RegisterTomlParser<SkillDefinitions>`).

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
| `Description` | string? (localization key) | `ItemDefinition.Description` |
| `Icon` | string? | `ItemDefinition.Icon` (optional override. Brick and blueprint placeables without `Icon` render an isometric 3D icon at startup through `BrickIconRenderer`) |
| `MaxStack` | int? | `ItemDefinition.MaxStack` |
| `[Items.Placeable]` | sub-table | `PlaceableDefinition` (`Type`, `ID`) |
| `[Items.Tool]` | sub-table | `ToolDefinition` (`Type`, `Target`) |
| `[Items.ViewModel]` | sub-table | `ModelDefinition` |
| `[Items.WorldModel]` | sub-table | `ModelDefinition` |

### Placeable

`PlaceableDefinition` (`WaywardBeyond.Items/PlaceableDefinition.cs`) pairs a
`Type` (`PlaceableType`, `WaywardBeyond.Items/PlaceableType.cs:4`) with an `ID`.
`ItemPlaceables` (`WaywardBeyond.Items/ItemPlaceables.cs:10`) resolves both
placeable kinds headlessly on client and server:

- `brick`: `ID` names a brick definition (for example `wb:panel`).
- `blueprint`: `ID` names a bundled `.wbp` blueprint relative to
  `assets/blueprints/` without the `.wbp` extension (for example `builtin/core`).
  The server resolves the blueprint through `IBundledBlueprintDatabase`
  (`WaywardBeyond.Data/Blueprints/IBundledBlueprintDatabase.cs:7`). See
  [region-blueprints](region-blueprints.md).

Examples:

```toml
[Items.Placeable]
Type = "brick"
ID = "wb:panel"
```

```toml
[Items.Placeable]
Type = "blueprint"
ID = "builtin/core"
```

### Tool

`ToolDefinition` (`ToolDefinition.cs`): `Type` (`ToolType`), `Target`. Item
group membership (`item.tool`, `item.tool.mining`, `item.tool.sprayer`,
`item.tool.drill`, `item.placeable`) lives in the invariant tag list
`WaywardBeyond.Items/assets/lang/tags/items.toml` (`ItemTags`).

### Model

`ModelDefinition` (`ModelDefinition.cs`): optional `Mesh` and `Material`, plus
`Position`/`Rotation`/`Scale` (each a `Float3` with `X`/`Y`/`Z`). `Scale`
defaults to `1,1,1`.

For `[Items.ViewModel]`, `Mesh` and `Material` are optional. When omitted,
`PlayerViewModelSystem` (`WaywardBeyond.Client/Systems/PlayerViewModelSystem.cs`)
infers the resolved voxel mesh and PBR voxel material from `[Items.Placeable]`
when the item places a brick or a bundled blueprint. Blueprint view models are
centered and normalized to fit a unit cube through `RegionMeshBuilder`. If no
explicit model or placeable resolves, the system falls back to a placeholder
cube using the missing-brick checkerboard texture layer
(`BrickPreviewMeshBuilder.BuildFallback`).

Example (placeable brick):

```toml
[Items.ViewModel]
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
| `Color` | string? | optional 6-digit hex RGB default albedo color. White when omitted |
| `Textures` | sub-table | `BrickTextures` |
| `Sounds` | `[Bricks.Sounds]` map | sound tags per `BrickEventKind` (`Place`, `Break`) |
| `Colliders` | `[[Bricks.Colliders]]` array | Optional `BrickColliderDefinition` list (`WaywardBeyond.Bricks/BrickColliderDefinition.cs`) |

Brick property and group membership (`brick.light`, `brick.entity`,
`brick.orientable`, `brick.paintable`, `brick.thruster`, `brick.buildable`,
`brick.environment`, `brick.manufactured`) lives in the invariant tag list
`WaywardBeyond.Bricks/assets/lang/tags/bricks.toml` (`BrickTags`). Brick TOMLs
do not declare inline `Tags`. `BrickDatabase` resolves `LightSource`, `Entity`,
`Paintable`, and `IsOrientable` once at load from `ILocalizedTagsDatabase.Invariant`.

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
[Bricks.Sounds]
Place = ["metal_place"]
Break = ["metal_break"]
[Bricks.Textures]
Default = ["core"]
```

### Sounds

`BrickDefinition.Sounds` is a map keyed by `BrickEventKind`
(`WaywardBeyond.Bricks/BrickEventKind.cs`) to invariant sound tags. The
`LocalizedTagsDatabase` list defines the tags, and each value is an audio clip
id. A brick without a declaration is silent. See
[sound-effects](sound-effects.md).

### Textures

`BrickTextures` (`BrickTextures.cs`): `Connected` plus per-face string?[] fields:
`Default`, `Top`, `Bottom`, `Front`, `Back`, `Left`, `Right`. `Default` is the
fallback face. Per-face entries override it.

Non-block shapes (`Slab`, `Stair`, `Slope`, `Column`, `Plate`, `Custom`) also
apply per-face textures in `MeshPostPass`
(`WaywardBeyond.Meshing/MeshPostPass.cs:601`). Each vertex
classifies its brick-local normal against the six cardinal faces in descending
axis magnitude, breaking ties in Z (`Front`/`Back`), Y (`Top`/`Bottom`), X
(`Right`/`Left`) order. When the primary face is unset, secondary axes with at
least half the dominant magnitude act as fallbacks before `Default`. A 45-degree
slope angled face therefore uses `Front`, falls back to `Top`, and then falls
back to `Default`.

### Color

`Color` is the brick's default albedo tint, authored as a 6-digit hex RGB string
with an optional leading `#`. `BrickDatabase` parses it once at load
(`WaywardBeyond.Bricks/BrickDatabase.cs`) into `Brick.Color` (sRGB) and
`Brick.LinearColor` (linear). An omitted color loads as white, so existing
fully colored textures keep their authored appearance. An invalid color logs a
warning and also falls back to white.

The tint multiplies every unpainted face by the brick texture, except where a
tint mask blocks it, so an artist can author a greyscale texture and let `Color`
supply the hue and value. A color-painted face uses its paint color, and a
texture-painted face uses the paint texture as authored, so a paint always shows
its exact color over the greyscale texture.

The tint reaches all voxel surfaces that resolve through
`ConnectedTextureResolver` (`WaywardBeyond.Meshing/ConnectedTextureResolver.cs`):
world chunks through `MeshPostPass`, and placement previews, held-item view
models, and baked brick item icons through `BrickPreviewMeshBuilder`. A light
brick emits `LightEntityDecorator.DefaultColor` multiplied by `Color` when
unpainted, and by the painted face color when painted.

Example (greyscale texture tinted steel blue):

```toml
[[Bricks]]
ID    = "wb:panel"
Color = "#4A6B8A"
[Bricks.Textures]
Default = ["panel"]
```

### Texture channels

Texture channel files use a suffix before `.png`: `.m` metallic, `.s`
smoothness, `.n` normal, `.e` emissive, and `.t` tint mask. The parser generates
a missing channel for every albedo texture, so each array has the same layer
order as `bricks_albedo`. A missing tint mask is white, which lets paint tint
every texel. Author a tint mask to keep painted colors off authored details.
See [asset-art-style](asset-art-style.md) and
[rendering-color](rendering-color.md).

### Colliders

Built-in shapes (`Block`, `Slab`, `Plate`, `Column`, `Stair`, `Slope`) resolve
their collision primitives from `BrickColliders`
(`WaywardBeyond.Bricks/BrickColliders.cs:8-63`). `Slope` uses a shared
6-vertex `ConvexHull`. `Stair` uses two boxes. A `Custom` brick without explicit
`[[Bricks.Colliders]]` falls back to a unit cube.

Any brick can declare one or more `[[Bricks.Colliders]]` tables in brick-local
space (centered on the voxel origin):

| Field | Type | Notes |
|---|---|---|
| `Type` | string | `box` (default), `sphere`, `capsule`, `cylinder`, or `hull` |
| `Size` | `{ X, Y, Z }` | Full extents for `box` (default `1, 1, 1`) |
| `Offset` | `{ X, Y, Z }` | Local position offset from voxel center |
| `Rotation` | `{ X, Y, Z }` | Local Euler rotation in degrees |
| `Radius` | float | Radius for `sphere`, `capsule`, or `cylinder` |
| `Height` | float | Height for `capsule` or `cylinder` |
| `Points` | `{ X, Y, Z }[]` | At least 4 vertices for `hull` |

Example (`WaywardBeyond.Bricks/assets/bricks/thruster.toml:16-24`):

```toml
[[Bricks.Colliders]]
Type   = "box"
Size   = { X = 1.0, Y = 1.0, Z = 0.1875 }
Offset = { X = 0.0, Y = 0.0, Z = -0.40625 }
```

Invalid collider entries log a warning and are skipped at `BrickDatabase` load
(`WaywardBeyond.Bricks/BrickDatabase.cs:96-117`). Both client
(`WaywardBeyond.Client/Voxels/Processing/CollisionPostPass.cs`) and server
(`WaywardBeyond.Server/Systems/VoxelColliderBuilder.cs`) build structure
colliders through `VoxelCollision` (`WaywardBeyond.Voxels/VoxelCollision.cs`).

## Paints (`assets/paints/*.toml`)

Schema: `WaywardBeyond.Bricks/PaintDefinitions.cs` (collection),
`PaintDefinition.cs` (row), `Paint.cs` (runtime asset), `PaintKind.cs`
(`Texture` or `Color`). Paint files live in `WaywardBeyond.Bricks/assets/paints/`
and load through `PaintDatabase` (`WaywardBeyond.Bricks/PaintDatabase.cs`).

Each `[[Paints]]` row defines either a texture paint (`Textures`) or a color
swatch (`Color`). A row that sets both or neither logs a warning and is skipped.

| Field | Type | Notes |
|---|---|---|
| `ID` | string | unique namespaced paint id (e.g. `wb:panel`, `wb:red`) |
| `Name` | string? | localization key for radial menu display |
| `Alpha` | `BrickAlpha` enum | `Opaque` (default), `Masked`, `Blend` (texture paints only) |
| `Connected` | bool | enables 4-bit connected-texture adjacency on painted faces |
| `Textures` | string?[]? | deterministic texture variant list (texture paints) |
| `Color` | string? | 6-digit hex RGB swatch (color paints) |

Paint palette and material group membership (`paint.endesga-32`, `paint.warm`,
`paint.metal`, and related groups) lives in the invariant tag list
`WaywardBeyond.Bricks/assets/lang/tags/paints.toml`. Paint TOMLs do not declare
inline `Tags`. Bricks opt in to player painting through the `brick.paintable`
invariant tag (`Brick.Paintable`).

## Materials (`assets/materials/**/*.toml`)

Flat config: `Swordfish/IO/MaterialDefinition.cs`. Keys are `Shader`,
`Textures`, `AlphaMode`, and `AlphaCutoff`. `AlphaMode` accepts `Opaque`,
`Masked`, or `Blend` and defaults to `Opaque`. `AlphaCutoff` sets the alpha
discard threshold for `Masked` materials and defaults to `0.5`. Example at
`assets/materials/bricks/glass.toml`. These are engine-side material
definitions, not game content tables.

## Particles (`assets/particles/**/*.toml`)

Particle effect TOML definitions load through `ParticleEffectDatabase`. See
[particles](particles.md) for the schema, color decoding, blend modes, and
simulation limits.

## Skills (`assets/skills/*.toml`)

Skills live in the shared `WaywardBeyond.Skills` module. The server owns XP
grants and level-up notifications, and the client renders notifications and the
skill panel. Schema: `WaywardBeyond.Skills/SkillDefinitions.cs` (collection),
`SkillDefinition.cs` (row), `XPSource.cs` (enum: `Place`, `Break`).

Each row is a `[[Skills]]` table:

| Field | Type | Notes |
|---|---|---|
| `ID` | string | |
| `Name` | string (localization key) | |
| `Category` | string (localization key) | groups rows in the client skill panel (`ui.label.skill.uncategorized` when omitted) |
| `Icon` | string? | skill artwork texture path under `skills/` (e.g. `skills/mining.png`). `SkillIconDatabase` resolves it through `IAssetDatabase<Texture>`, falls back to `skills/unknown.png`, and logs a warning on an unknown path |
| `[Skills.Sources.<Source>]` | map: brick id or `tag:X` → XP | keyed by `XPSource` (Place/Break) |
| `[Skills.Levels]` | map: level → XP to advance past the previous level | a level is reached only when total XP exceeds the running sum of requirements, and it is sorted on load |

Tags expand through the invariant `lang/tags/*.toml` lists into brick data ids
on load. The brick tag lists live in `WaywardBeyond.Bricks/assets/lang/tags/bricks.toml`
under `brick.*` keys (`brick.buildable`, `brick.environment`, `brick.manufactured`).
`Name` and `Category` are localization keys. The server sends `Name` in
notifications and the client resolves both `Name` and `Category` in the skill
panel. `SkillIconDatabase` resolves `Icon` on the client with fallback to
`skills/unknown.png` and a warning logged for an unknown path.

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
hosted in `WaywardBeyond.Data/Localization/`. Sound tags also live here, and
their values are audio clip ids. See [sound-effects](sound-effects.md).

Each tag stores its values in an ordered set. A value appears once, in the order
the files first declare it. `LocalizedTagsDatabase` deduplicates on merge and
appends the invariant values after the language's own values. `LocalizedTags.Contains`
is an O(1) set lookup used by the invariant brick, paint, and item tag checks.
`SeedPhraseService` depends on the order because it indexes the values by seed
bits. See [seed-phrases](seed-phrases.md).

## Source of truth

- `WaywardBeyond.Items/{ItemDefinitions,ItemDefinition,ItemTags,ToolDefinition,PlaceableDefinition,PlaceableType,ModelDefinition,ItemDatabase,ItemPlaceables}.cs`
- `WaywardBeyond.Data/Blueprints/{IBundledBlueprintDatabase,BundledBlueprintDatabase,BlueprintDataParser}.cs`
- `WaywardBeyond.Bricks/{BrickDefinitions,BrickDefinition,BrickTags,BrickTextures,BrickAlpha,BrickColliderDefinition,BrickColliderShape,BrickColliders}.cs`
- `WaywardBeyond.Bricks/{Brick,Srgb}.cs`
- `WaywardBeyond.Voxels/VoxelCollision.cs`
- `WaywardBeyond.Skills/{SkillDefinitions,SkillDefinition,XPSource,Skill,SkillProgress}.cs`
- `WaywardBeyond.Client/Skills/SkillIconDatabase.cs`
- `WaywardBeyond.Bricks/BrickEventKind.cs`
- `WaywardBeyond.Data/Localization/LocalizedTagsDefinition.cs`
- `WaywardBeyond.Data/Localization/LocalizedTagValues.cs`
- `WaywardBeyond.Bodies/{BodyDefinitions,BodyDefinition,Body,Gender,BodyDatabase}.cs`
- `WaywardBeyond.Bricks/BrickShape.cs`
- `WaywardBeyond.Client/Graphics/PBRTextureArraysParser.cs`
- `Swordfish/IO/MaterialDefinition.cs`
- Parser registration: `WaywardBeyond.Skills/Injector.cs:16`,
  `WaywardBeyond.Client/Injector.cs:343`, `WaywardBeyond.Bricks/Injector.cs:14`,
  `WaywardBeyond.Bricks/Injector.cs:20`

## Tests that pin this

`WaywardBeyond.Tests/SkillDatabaseTests.cs` loads the shipped skill and brick tomls
and pins the schema, the tag expansion, and the level curve. See
[skills](skills.md). `WaywardBeyond.Tests/BodyDatabaseTests.cs` loads the shipped
body definitions and pins the species order, the gender groups, the species
carried on each loaded body, and the species and gender resolution.
`WaywardBeyond.Tests/BrickColliderTests.cs` pins TOML `[[Bricks.Colliders]]`
parsing, `wb:thruster` colliders, and non-cube raycast cell resolution.
`WaywardBeyond.Voxels.Tests/VoxelCollisionTests.cs` pins built-in `BrickShape`
colliders, orientation transforms, and mixed-shape interior culling.
`WaywardBeyond.Tests/ShapedBrickTextureTests.cs` pins per-face texture mapping,
angled-face fallback, rotation invariance, and default fallback on non-block
shapes.
`WaywardBeyond.Tests/PlayerViewModelTests.cs` pins optional `ViewModel` mesh and
material fields, placeable brick and blueprint view-model inference, shape
updates, PBR shadow options, and the placeholder checkerboard fallback.
`WaywardBeyond.Tests/ItemPlaceablesTests.cs` pins brick and blueprint placeable
resolution, `wb:core`, and bundled blueprint loading.
`WaywardBeyond.Tests/BrickIconTests.cs` pins isometric 3D icon baking for both
brick and blueprint placeable items.
`WaywardBeyond.Tests/LocalizedTagsTests.cs` pins tag value deduplication, first
occurrence order, and the invariant merge into each language.
