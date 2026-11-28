# Asset Definition TOML Formats

One subject: the TOML config types that define game content (items, bricks,
materials, recipes, skills, HUD bars, buffs). Item definitions live under
`WaywardBeyond.Items/assets/`. Brick definitions live in
`WaywardBeyond.Bricks/assets/`. Recipe definitions load from `recipes/` across
mounted modules such as `WaywardBeyond.Recipes/`. Skill definitions live in
`WaywardBeyond.Skills/assets/`. HUD bar definitions load from `hud_bars/` and
buff definitions load from `buffs/` across mounted modules. Brick and item
localization stays client-owned.

Parser registration: `WaywardBeyond.Items/Injector.cs:15`
(`RegisterTomlParser<ItemDefinitions>`), `WaywardBeyond.Bricks/Injector.cs:17`
(`RegisterTomlParser<BrickDefinitions>`), `WaywardBeyond.Recipes/Injector.cs:17`
(`RegisterTomlParser<RecipeDefinitions>`), `WaywardBeyond.Skills/Injector.cs:16`
(`RegisterTomlParser<SkillDefinitions>`), and
`WaywardBeyond.Data/DataComposition.cs:28-29` (`RegisterHudBars`, `RegisterBuffs`).

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
| `[Items.Statistics]` | map: string → float | `ItemDefinition.Statistics` (custom numeric statistics such as `"wb:mining_speed"`) |
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

`ToolDefinition` (`WaywardBeyond.Items/ToolDefinition.cs`): `Type` (`ToolType`)
and optional `Target`. Item group membership (`item.tool`, `item.tool.mining`,
`item.tool.sprayer`, `item.tool.drill`, `item.placeable`) lives in the invariant
tag list `WaywardBeyond.Items/assets/lang/tags/items.toml` (`ItemTags`).

### Statistics

`ItemDefinition.Statistics` (`WaywardBeyond.Items/ItemDefinition.cs:19`) is an
optional `[Items.Statistics]` key-value map from statistic id to finite `float`
value. Base-game keys use the `wb:` namespace (for example `"wb:mining_speed"`).
Mods add their own namespaced keys without changing source code. `ItemDatabase`
(`WaywardBeyond.Items/ItemDatabase.cs:59`) logs a warning and skips empty keys
or non-finite values. `WorkspaceValidator` emits `STAT001` for invalid item
statistic entries, including non-positive `"wb:mining_speed"` values. See
[statistics](statistics.md).

Example:

```toml
[Items.Statistics]
"wb:mining_speed" = 1.75
```

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
| `Statistics` | `[Bricks.Statistics]` map: string → float | `BrickDefinition.Statistics` (custom numeric statistics such as `"wb:hardness"`) |

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

### Statistics

`BrickDefinition.Statistics` (`WaywardBeyond.Bricks/BrickDefinition.cs:39`) is an
optional `[Bricks.Statistics]` key-value map from statistic id to finite `float`
value, matching `[Items.Statistics]`. `"wb:hardness"` sets a brick's base break
resistance in seconds before mining speed modifiers apply (default `1.0`, `0.0`
breaks in zero seconds). `BrickDatabase` (`WaywardBeyond.Bricks/BrickDatabase.cs:137`)
logs a warning and skips empty keys or non-finite values. `WorkspaceValidator`
emits `STAT001` for invalid brick statistic entries, including negative
`"wb:hardness"` values. See [statistics](statistics.md).

Example:

```toml
[Bricks.Statistics]
"wb:hardness" = 2.5
```

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

## Inventory slot groups (`assets/inventory/*.toml`)

Slot group schemas live in `WaywardBeyond.Items/assets/inventory/` and load through
`SlotGroupSchemaDatabase` (`WaywardBeyond.Data/Inventory/SlotGroupSchemaDatabase.cs:14`),
registered via `DataComposition.RegisterDataServices` (`WaywardBeyond.Data/DataComposition.cs:23`).
Schema structs live in `SlotGroupDefinitions.cs`, `SlotGroupDefinition.cs`, and
`SlotFilterDefinition.cs`. The loaded immutable asset is `SlotGroupSchema`
(`WaywardBeyond.Data/Inventory/SlotGroupSchema.cs:8`).

Each row is a `[[SlotGroups]]` table with optional `[[SlotGroups.Slots]]` per-slot
filter overrides:

| Field | Type | Notes |
|---|---|---|
| `ID` | string | unique namespaced slot group identifier (e.g. `wb:wearables`) |
| `Capacity` | int | default slot count for the group (`>= 0`) |
| `AllowedTags` | string[]? | optional invariant item tag keys permitted across the group |
| `Slots` | `[[SlotGroups.Slots]]` | optional per-slot overrides with `Index` (`>= 0`) and `AllowedTags` |

See [networking-inventory](networking-inventory.md) for group IDs, filter enforcement,
and transfer resolution.

## Recipes (`assets/recipes/*.toml`)

Recipes live in the shared `WaywardBeyond.Recipes` module. The module loads
definitions headlessly through `RecipeDatabase`
(`WaywardBeyond.Recipes/RecipeDatabase.cs:18`) and exposes `IRecipeDatabase`
(`WaywardBeyond.Recipes/IRecipeDatabase.cs:11`). Schema types live in
`RecipeDefinitions.cs:7`, `RecipeDefinition.cs:7`,
`RecipeIngredientDefinition.cs:6`, and `RecipeOutputDefinition.cs:6`. Runtime
value types are `Recipe` (`Recipe.cs:7`), `RecipeIngredient`
(`RecipeIngredient.cs:8`), `RecipeOutput` (`RecipeOutput.cs:4`), and
`RecipeMode` (`RecipeMode.cs:4`).

Each row is a `[[Recipes]]` table:

| Field | Type | Notes |
|---|---|---|
| `ID` | string | unique namespaced recipe identifier |
| `TargetTag` | string | invariant target tag identifying stations or targets that run the recipe |
| `Mode` | `RecipeMode` | `Auto` or `Manual` (`Manual` when omitted) |
| `Inputs` | `[[Recipes.Inputs]]` | at least one entry with `ItemId` (item id or `tag:<name>`), `Count >= 1`, and optional `SlotGroup` |
| `Outputs` | `[[Recipes.Outputs]]` | entries with `ItemId`, `Count >= 1`, and optional `Chance` in `(0, 1]` (`1` when omitted) |
| `DurationSeconds` | float | processing time in seconds (`>= 0`) |
| `PowerKw` | float | negative draws power, positive generates power (generator recipes may omit `Outputs` when `PowerKw > 0`) |
| `Category` | string? | optional category key |
| `Priority` | int | deterministic priority within a target list (lower values sort first) |

Recipe files never declare skill XP or skill IDs. Skills own their crafting and
processing XP rules in `assets/skills/*.toml`. See [skills](skills.md).

`RecipeDatabase` validates every item reference against `IAssetDatabase<Item>`
and expands `tag:<name>` inputs through `ILocalizedTagsDatabase.Invariant` at
load time. Invalid rows log an error and skip without crashing.
`GetByTargetTag` returns recipes sorted by `Priority` then `ID`. `FindAutoMatch`
matches `RecipeMode.Auto` recipes against `ReadOnlySpan<ItemData>` or
`in InventoryComponent` without heap allocations, prioritizing exact item
ingredients before tag ingredients. `IsValidInputForStation` checks whether any
recipe for that station accepts an item id.

## Skills (`assets/skills/*.toml`)

Skills live in the shared `WaywardBeyond.Skills` module. The server owns XP
grants and level-up notifications, and the client renders notifications and the
skill panel. Schema: `WaywardBeyond.Skills/SkillDefinitions.cs` (collection),
`SkillDefinition.cs` (row), `XPSource.cs` (`Place`, `Break`, `Craft`, `Process`).

Each row is a `[[Skills]]` table:

| Field | Type | Notes |
|---|---|---|
| `ID` | string | |
| `Name` | string (localization key) | |
| `Category` | string (localization key) | groups rows in the client skill panel (`ui.label.skill.uncategorized` when omitted) |
| `Icon` | string? | skill artwork texture path under `skills/` (e.g. `skills/mining.png`). `SkillIconDatabase` resolves it through `IAssetDatabase<Texture>`, falls back to `skills/unknown.png`, and logs a warning on an unknown path |
| `[Skills.Sources.<Source>]` | map: target selector → XP | `Place`/`Break` map brick ids or `tag:X`. `Craft`/`Process` map `station:<tag>`, `recipe:<id>`, `tag:<itemTag>`, or direct recipe/output item ids |
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

## HUD Bars (`assets/hud_bars/*.toml`)

HUD bars live in the shared `WaywardBeyond.Data` module under `WaywardBeyond.Data/HudBars/`. `DataComposition.RegisterDataServices` (`WaywardBeyond.Data/DataComposition.cs:26`) registers `IHudBarDatabase` and `IAssetDatabase<HudBarDefinition>`. `HudBarDefinitionsParser` accepts flat single-bar tables or `[[hud_bars]]` table arrays.

| Field | Type | Notes |
|---|---|---|
| `id` | string | required unique bar id (for example `wb:health`) |
| `stat_id` | string | required current value stat key |
| `max_stat_id` | string? | optional max value stat key |
| `default_max` | float? | optional finite positive fallback maximum when `max_stat_id` is omitted or absent |
| `icon_count` | int | positive integer icon count (defaults to `10`) |
| `icon_full` | string | required texture asset key for full icons |
| `icon_half` | string | required texture asset key for half icons |
| `icon_empty` | string | required texture asset key for empty icons |
| `warn_threshold` | float | ratio in `[0, 1]` activating warning state (defaults to `0.3`) |
| `critical_threshold` | float | ratio in `[0, 1]` activating critical state and `<= warn_threshold` (defaults to `0.15`) |
| `order` | int | vertical stack order with lower values first (defaults to `0`) |
| `hide_in_creative` | bool | hides the bar in Creative mode (defaults to `false`) |
| `color_tint` | string? | optional hex sRGBA color parsed through `HexColor.TryParseRgba` |

Validation through `HudBarDefinition.Validate` (`WaywardBeyond.Data/HudBars/HudBarDefinition.cs:61-137`) returns `Result<HudBarDefinition>`. It rejects blank ids, blank stat or icon keys, missing `max_stat_id` without a positive finite `default_max`, `icon_count < 1`, thresholds outside `[0, 1]`, `critical_threshold > warn_threshold`, and invalid hex tints.

## Buffs (`assets/buffs/*.toml`)

Buff definitions live in the shared `WaywardBeyond.Data` module under `WaywardBeyond.Data/Buffs/`. `DataComposition.RegisterDataServices` (`WaywardBeyond.Data/DataComposition.cs:27`) registers `IBuffDatabase` and `IAssetDatabase<BuffDefinition>`. `BuffDefinitionsParser` accepts flat single-buff tables or `[[buffs]]` table arrays.

| Field | Type | Notes |
|---|---|---|
| `id` | string | required unique buff id (for example `wb:well_fed`) |
| `name` | string (localization key) | required display name key |
| `description` | string? (localization key) | optional description key |
| `icon` | string? | optional buff icon texture key |
| `duration_seconds` | float | required finite duration `> 0` |
| `stack_policy` | string | `refresh` (default), `extend`, or `ignore` |
| `[[stat_modifiers]]` | table array | rows of `stat_id` with `flat_delta`, `percent_delta`, or both (`!= 0`) |
| `[regen]` / `[regen_rates]` | table or top-level keys | finite `health_per_sec` and `oxygen_per_sec` regen rates |

A buff cannot specify both top-level regen keys and a regen sub-table, or both `[regen]` and `[regen_rates]` (`WaywardBeyond.Data/Buffs/BuffDefinitionsParser.cs:159-185`). `BuffDefinition.Validate` (`WaywardBeyond.Data/Buffs/BuffDefinition.cs:43-112`) returns `Result<BuffDefinition>` and rejects blank ids or names, non-positive durations, invalid stack policies, non-finite regen rates, and modifiers lacking a stat key or non-zero finite delta. Duplicate alias keys within one table or documents mixing `[[buffs]]`/`[[hud_bars]]` with top-level keys are rejected by `TomlTableReader` (`WaywardBeyond.Data/TomlTableReader.cs:10-82`).

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
- `WaywardBeyond.Data/Inventory/{ISlotGroupSchemaDatabase,SlotGroupSchemaDatabase,SlotGroupSchema,SlotGroupDefinitions,SlotGroupDefinition,SlotFilterDefinition,SlotGroups}.cs`
- `WaywardBeyond.Recipes/{RecipeDefinitions,RecipeDefinition,RecipeIngredientDefinition,RecipeOutputDefinition,RecipeMode,Recipe,RecipeIngredient,RecipeOutput,IRecipeDatabase,RecipeDatabase,Injector}.cs`
- `WaywardBeyond.Skills/{SkillDefinitions,SkillDefinition,XPSource,CraftXpSourceSet,Skill,SkillDatabase,SkillProgress}.cs`
- `WaywardBeyond.Client/Skills/SkillIconDatabase.cs`
- `WaywardBeyond.Bricks/BrickEventKind.cs`
- `WaywardBeyond.Data/Localization/LocalizedTagsDefinition.cs`
- `WaywardBeyond.Data/Localization/LocalizedTagValues.cs`
- `WaywardBeyond.Data/HudBars/{HudBarDefinition,HudBarEntry,HudBarDefinitions,HudBarDefinitionsParser,IHudBarDatabase,HudBarDatabase,HudBarComposition}.cs`
- `WaywardBeyond.Data/Buffs/{BuffDefinition,BuffEntry,BuffDefinitions,BuffDefinitionsParser,BuffStackPolicy,BuffStatModifier,IBuffDatabase,BuffDatabase,BuffComposition}.cs`
- `WaywardBeyond.Data/{DataComposition,TomlTableReader}.cs`
- `WaywardBeyond.Bodies/{BodyDefinitions,BodyDefinition,Body,Gender,BodyDatabase}.cs`
- `WaywardBeyond.Bricks/BrickShape.cs`
- `WaywardBeyond.Client/Graphics/PBRTextureArraysParser.cs`
- `Swordfish/IO/MaterialDefinition.cs`
- Parser registration: `WaywardBeyond.Items/Injector.cs:15`,
  `WaywardBeyond.Recipes/Injector.cs:17`,
  `WaywardBeyond.Skills/Injector.cs:16`,
  `WaywardBeyond.Client/Injector.cs:382`, `WaywardBeyond.Bricks/Injector.cs:17`,
  `WaywardBeyond.Bricks/Injector.cs:23`,
  `WaywardBeyond.Data/DataComposition.cs:26-27`

## Tests that pin this

`WaywardBeyond.Tests/RecipeDatabaseTests.cs` pins recipe TOML parsing, item and
tag validation errors, `GetByTargetTag` ordering, `FindAutoMatch`,
`IsValidInputForStation`, and DI registration. `WaywardBeyond.Tests/SkillDatabaseTests.cs` loads the shipped skill and brick tomls
and pins the schema, the tag expansion, crafting and processing XP sources, and
the level curve. See [skills](skills.md). `WaywardBeyond.Tests/BodyDatabaseTests.cs` loads the shipped
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
`WaywardBeyond.Tests/TimedBrickBreakingTests.cs` and
`WaywardBeyond.AssetTools.Tests/Validation/ValidationEngineTests.cs` pin
`[Bricks.Statistics]` and `[Items.Statistics]` parsing, fallback defaults, and
`STAT001` validation findings.
`WaywardBeyond.Tests/HudBarDatabaseTests.cs` pins `hud_bars/*.toml` table-array
and flat parsing, defaults, `ColorTint` parsing, ordering, and validation failures.
`WaywardBeyond.Tests/BuffDatabaseTests.cs` pins `buffs/*.toml` parsing, stack
policies, stat modifiers, regen tables, conflict detection, and validation
failures.
