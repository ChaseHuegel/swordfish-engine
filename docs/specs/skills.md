# Skills — Server-Authoritative Model

One subject: how skill definitions and skill XP work across the client/server
boundary.

## Model

The server owns every gameplay aspect of skills for the life of a session. It
grants XP, sums it per skill, detects level-ups, and pushes notifications. The
client seeds its saved skill data at join and then acts as a dumb display: it
renders notifications the server sends and records the authoritative totals into
its local character save. No skill math, XP prediction, or level calculation runs
client-side.

Definitions and mechanics live in a shared module. The server resolves them to
grant XP (sources, level curves). The client module runs no skill math. It
localizes only the notification keys the server sends, and skill names are
localization keys.

## Definitions

`WaywardBeyond.Skills/` is a Shoal module (`waywardbeyond.skills`)
that owns the headless models and the tomls:

- `XPSource.cs` — `XPSource { Place, Break }`.
- `SkillDefinition.cs`, `SkillDefinitions.cs` — the TOML schema.
  Sources stay readable as `"wb:rock" = 1` or `"tag:buildable" = 1`.
- `Skill.cs` — the runtime asset: ID, name/category localization keys, icon
  path, level curve (`SortedDictionary`), and sources keyed by brick
  **data id**. `GetXP(XPSource, ushort)` resolves the XP for a brick.
  `CalculateLevel(long)` resolves the level for a total XP amount. A level is
  reached only when the total XP exceeds the sum of every requirement up to it,
  so an exact threshold reports the lower level.
- `SkillDatabase.cs` — headless loader (`VirtualAssetDatabase`). Expands
  `tag:&lt;name&gt;` sources into brick data ids through an injected
  `IBrickRegistry` (the same map the client `BrickDatabase` owns, so ids match
  worldgen and placement), logs a warning on an unknown brick id, an unknown
  tag, or a brick listed twice in one source set, and caches the skills per
  `XPSource` for the interaction hot path. It reads only the invariant gameplay
  tag list from `ILocalizedTagsDatabase` (see
  [asset-definitions](asset-definitions.md)). It never resolves display
  localization, textures, or icons. It rejects a skill with no ID, a duplicate
  ID, no level curve, or a level requirement below 1.
- `ISkillDatabase.cs` — the public contract over the loaded `Skill`s.
- `LevelInfo.cs` — the resolved level plus the XP earned into that level.
- The transient, in-memory per-player XP table lives server-side as
  `WaywardBeyond.Server/Components/SkillStateComponent.cs`. It is never
  persisted and never replicated as a component.

Assets: `assets/skills/*.toml` (skill definitions) and `assets/lang/tags/*.toml`
(invariant gameplay tag lists, empty-language). Tag list files are general
purpose: the shared tag schema, database, and parser live in
`WaywardBeyond.Data/Localization/` and serve both gameplay (skills) and client
presentation (name generation, load screen).

The `Name`/`Category` fields are **localization keys**, not display strings. The
client's `assets/lang/en/skills.toml` holds the display strings.

## Authority

- **Join seed**: the client sends its saved statistics on the `CharacterState`
  (`Statistic[]? Statistics`). `ServerJoinSystem` filters them to known skill ids
  and stores them in the player's transient `SkillStateComponent`. New characters
  seed an empty component.
- **Gameplay**: `ServerInteractionSystem` calls
  `ServerSkillSystem.OnInteractionApplied` for each accepted break/place with the
  involved brick's data id. It accumulates XP, computes the level, and sends
  `SkillStateUpdateMessage` (authoritative totals) plus `NotificationMessage`
  (display) to that player's client.
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
its own locale files before formatting. `notification.skill.bar` renders the XP
progress bar; `notification.skill.levelUp` renders the level-up toast. The bar
amount is the XP earned into the current level divided by the next level's
increment; a max-level skill renders a full bar.

## Source of truth

- `WaywardBeyond.Skills/*.cs`
- `WaywardBeyond.Skills/assets/{skills,lang/tags}/**`
- `WaywardBeyond.Data/Localization/{LocalizedTagsDefinition,LocalizedTags,LocalizedTagsDatabase,LocalizedTagDefinitionParser,ILocalizedTagsDatabase,LocalizationComposition}.cs`
- `WaywardBeyond.Server/Components/SkillStateComponent.cs`
- `WaywardBeyond.Server/Systems/ServerSkillSystem.cs`
- `WaywardBeyond.Server/Systems/ServerJoinSystem.cs` (seed)
- `WaywardBeyond.Client/Systems/ClientNotificationSystem.cs`
- `WaywardBeyond.Client/assets/lang/en/{skills,notification}.toml`
- `WaywardBeyond.Data/Nsd/saves.nsd` (`Statistic`)

## Tests that pin this

- `Swordfish.Tests/SkillDatabaseTests.cs` — the shipped definitions load, tag
  sources expand to the brick data ids worldgen writes, skills group by
  `XPSource`, the curve stays sorted, and the level thresholds and rejection
  paths hold.
- `Swordfish.Tests/ServerSkillSystemTests.cs` — the authoritative grant
  accumulates into `SkillStateComponent`, an unknown brick grants nothing, and
  a seeded total continues the curve.
