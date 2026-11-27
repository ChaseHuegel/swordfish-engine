# Skills — Server-Authoritative Model

One subject: how skill definitions and skill XP work across the client/server
boundary.

## Model

The server owns every gameplay aspect of skills for the life of a session. It
grants XP, sums it per skill, detects level-ups, and pushes notifications. The
client seeds its saved skill data at join, records authoritative XP totals into
its local character save, renders notifications the server sends, and displays
all skills in the skill panel. The client never predicts or grants XP.

Definitions and mechanics live in a shared module. The server resolves them to
grant XP (sources, level curves). The client reads the shared definitions to
display every skill, compute read-only level and bar progress from the saved
authoritative XP totals, resolve skill icons, and localize skill names,
categories, and notification keys.

## Definitions

`WaywardBeyond.Skills/` is a Shoal module (`waywardbeyond.skills`)
that owns the headless models and the tomls:

- `XPSource.cs` — `XPSource { Place, Break, Craft, Process }`
  (`WaywardBeyond.Skills/XPSource.cs:4`).
- `SkillDefinition.cs`, `SkillDefinitions.cs` — the TOML schema.
  Brick sources use `"wb:rock" = 1` or `"tag:brick.buildable" = 1`.
  Crafting and processing sources under `[Skills.Sources.Craft]` and
  `[Skills.Sources.Process]` use `station:<tag>`, `recipe:<id>`, `tag:<itemTag>`,
  or direct recipe and output item ids. Recipe files never embed skill XP.
- `CraftXpSourceSet.cs` — resolved crafting and processing XP lookup set
  (`WaywardBeyond.Skills/CraftXpSourceSet.cs:8`). `Evaluate` resolves XP in
  precedence order: explicit `recipe:<id>`, direct `recipeId`, direct output
  item id, expanded output item tag, then `station:<tag>`.
- `Skill.cs` — the runtime asset: ID, name/category localization keys, icon
  path, level curve (`SortedDictionary`), brick sources keyed by brick
  **data id**, and optional craft/process source sets. `GetXP(XPSource, ushort)`
  (`WaywardBeyond.Skills/Skill.cs:66`) resolves brick XP.
  `GetXP(XPSource, string, string, string?)` (`WaywardBeyond.Skills/Skill.cs:79`)
  and `GetXP(XPSource, string, string, ReadOnlySpan<string>)`
  (`WaywardBeyond.Skills/Skill.cs:91`) resolve crafting and processing XP
  without referencing `WaywardBeyond.Recipes`. `CalculateLevel(long)` resolves
  the level for a total XP amount. A level is reached only when the total XP
  exceeds the sum of every requirement up to it, so an exact threshold reports
  the lower level. `CalculateProgress(long)` returns `SkillProgress` with the
  resolved level, total XP, XP into the level, next-level requirement,
  normalized bar progress, and max-level flag.
- `SkillDatabase.cs` — headless loader (`VirtualAssetDatabase`,
  `WaywardBeyond.Skills/SkillDatabase.cs:14`). Expands `tag:<name>` brick
  sources into brick data ids through `IBrickRegistry` and expands `tag:<name>`
  craft/process sources into item ids through `ILocalizedTagsDatabase.Invariant`
  (see [asset-definitions](asset-definitions.md)). It logs a warning on an
  unknown brick id, an unknown tag, an empty selector, or a duplicate entry in
  one source set. It caches skills per `XPSource` in deterministic ID order and
  exposes `All` in deterministic ID order. It never resolves display
  localization, textures, or icons. It rejects a skill with no ID, a duplicate
  ID, no level curve, or a level requirement below 1.
- `ISkillDatabase.cs` — the public contract over the loaded `Skill`s (`All`,
  `Get(string)`, and `Get(XPSource)`).
- `LevelInfo.cs` — the resolved level plus the XP earned into that level.
- `SkillProgress.cs` — the resolved level, total XP, XP earned into the level,
  next-level requirement, normalized bar progress, and max-level state.
- The transient, in-memory per-player XP table lives server-side as
  `WaywardBeyond.Server/Components/SkillStateComponent.cs`. It is never
  persisted and never replicated as a component.

Assets: `assets/skills/*.toml` (skill definitions). Invariant brick tag lists
live in `WaywardBeyond.Bricks/assets/lang/tags/bricks.toml` (`brick.buildable`,
`brick.environment`, `brick.manufactured`). Tag list files are general
purpose: the shared tag schema, database, and parser live in
`WaywardBeyond.Data/Localization/` and serve bricks, paints, items, skills,
client presentation (name generation, load screen), and sound effects.

The `Name`/`Category` fields are **localization keys**, not display strings. The
client's `assets/lang/en/skills.toml` holds the display strings.

## Authority

- **Join seed**: the client sends its saved statistics on the `CharacterState`
  (`Statistic[]? Statistics`). `ServerJoinSystem` filters them to known skill ids
  and stores them in the player's transient `SkillStateComponent`. New characters
  seed an empty component.
- **Gameplay**: `SkillXpReactor` (the pipeline's `React` stage) grants XP for
  each accepted break/place with the involved brick's data id. It accumulates
  XP, computes the level, and sends `SkillStateUpdateMessage` (authoritative
  totals) plus `NotificationMessage` (display) to that player's client.
- **Persistence**: only the client persists. Each `SkillStateUpdateMessage`
  writes the authoritative total into the client-owned `Character.Statistics`,
  which the normal autosave persists. The server stores no character skill data
  at all; the component lives for the session and is freed with the player
  mirror.

## Notifications

Servers push `NotificationMessage`s (see
[networking-messages](networking-messages.md)). The message carries a
localization **key** and positional args; servers never localize. The client's
`ClientNotificationSystem` resolves any arg that names a localization key through
its own locale files before formatting. `notification.skill.bar` renders the
left-aligned skill name and level on the XP progress bar, and
`notification.skill.bar.subtext` renders the right-aligned gained XP and
percentage; `notification.skill.levelUp` renders the level-up toast. The bar
amount is the XP earned into the current level divided by the next level's
increment; a max-level skill renders a full bar. Skill XP bars set `Segments`
to 10 and leave `Color` null so `Bars.cs` uses the default accent fill.

## Skill panel and icon fallback

`SkillsLayer` (`WaywardBeyond.Client/UI/Layers/Skills/SkillsLayer.cs:19`)
renders the in-game modal skill panel. The `UI/Skills` shortcut (`K` by default,
rebindable) toggles the panel while playing, and `General/Back` (`Esc`) or the
close button closes it. The panel acquires an exclusive `InteractionBlocker`
while open.

The panel lists every skill from `ISkillDatabase.All` and groups the rows by
`Skill.Category` (falling back to `ui.label.skill.uncategorized` when a skill
omits `Category`). Each row reads the active character's authoritative total XP
from `ActiveCharacterSave`, resolves level, XP into the level, next-level
requirement, and bar fill through `Skill.CalculateProgress`, and localizes
`Skill.Name` and `Skill.Category` through `SkillsView`.

`SkillIconDatabase` (`WaywardBeyond.Client/Skills/SkillIconDatabase.cs:15`)
resolves `Skill.Icon` through `IAssetDatabase<Texture>` and caches one UI
`Material` per texture path. When `Skill.Icon` is empty or does not exist in the
texture database, `SkillIconDatabase` falls back to `skills/unknown.png`
(`WaywardBeyond.Client/assets/textures/skills/unknown.png`). An unknown
non-empty path logs a warning once and caches the fallback material. Shipped
skill artwork lives under `WaywardBeyond.Client/assets/textures/skills/`.

## Source of truth

- `WaywardBeyond.Skills/*.cs`
- `WaywardBeyond.Skills/assets/skills/**`
- `WaywardBeyond.Bricks/assets/lang/tags/bricks.toml`
- `WaywardBeyond.Data/Localization/{LocalizedTagsDefinition,LocalizedTags,LocalizedTagValues,LocalizedTagsDatabase,LocalizedTagDefinitionParser,ILocalizedTagsDatabase,LocalizationComposition}.cs`
- `WaywardBeyond.Server/Components/SkillStateComponent.cs`
- `WaywardBeyond.Server/Interactions/SkillXpReactor.cs`
- `WaywardBeyond.Server/Systems/ServerJoinSystem.cs` (seed)
- `WaywardBeyond.Client/Systems/ClientNotificationSystem.cs`
- `WaywardBeyond.Client/Skills/SkillIconDatabase.cs`
- `WaywardBeyond.Client/UI/Layers/Skills/{SkillsLayer,SkillsView,SkillsPanelState,SkillCategoryGroupState,SkillRowState}.cs`
- `WaywardBeyond.Client/UI/Layers/Skills/{SkillsView.rml,SkillsView.rss}`
- `WaywardBeyond.Client/assets/textures/skills/**`
- `WaywardBeyond.Client/assets/lang/en/{skills,notification,ui}.toml`
- `WaywardBeyond.Data/Nsd/saves.nsd` (`Statistic`)

## Tests that pin this

- `WaywardBeyond.Tests/SkillDatabaseTests.cs` — the shipped definitions load,
  `All` returns skills in deterministic order with `skills/` artwork icons, tag
  sources expand to the brick data ids worldgen writes, crafting and processing
  sources evaluate station tags, recipe ids, and output item tags by precedence,
  skills group by `XPSource`, the curve stays sorted, and the level thresholds
  and rejection paths hold.
- `WaywardBeyond.Tests/SkillsPanelTests.cs` — `Skill.CalculateProgress` and the
  panel rows match `Skill.CalculateLevel` and bar math across thresholds,
  `SkillIconDatabase` resolves textures, falls back to `skills/unknown.png`, and
  logs a warning once on an unknown path, and `SkillsLayer` groups skills by
  category and manages toggle, escape, and close lifecycle.
- `WaywardBeyond.Tests/SkillXpReactorTests.cs` — the authoritative grant
  accumulates into `SkillStateComponent`, an unknown brick grants nothing, and
  a seeded total continues the curve.
- `WaywardBeyond.Tests/ServerJoinSkillSeedTests.cs` — the join seed carries known
  skill statistics and drops every other statistic.
