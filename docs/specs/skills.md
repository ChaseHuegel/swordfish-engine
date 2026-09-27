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

Definitions and mechanics live in a shared module so both sides resolve the same
data:
- The **server** reads them to grant XP (sources, level curves).
- The **client** reads nothing for gameplay; it only localizes the notification
  keys the server sends (skill names and categories are localization keys).

## Definitions

`WaywardBeyond.Shared.Skills/` is a Shoal module (`waywardbeyond.shared.skills`)
that owns the headless models and the tomls:

- `Skills/XPSource.cs` — `Source { Place, Break }`.
- `Skills/SkillDefinition.cs`, `Skills/SkillDefinitions.cs` — the TOML schema.
  Sources stay readable as `"rock" = 1` or `"tag:buildable" = 1`.
- `Skills/SkillData.cs` — the runtime asset: ID, name/category localization
  keys, icon path, max level, sorted level curve, and sources keyed by brick
  **data id** (`Dictionary<XPSource, Dictionary<ushort,int>>`).
- `Skills/SkillDatabase.cs` — headless loader (`VirtualAssetDatabase`). Expands
  the invariant `lang/tags/` lists into brick data ids via
  `FNV1a.ComputeDataID` (same rule as `WorldMaterialCatalog`), logs a warning on
  a source-set data-id collision, and never touches localization, textures, or
  icons.
- `Skills/SkillDataExtensions.cs` — `CalculateLevel(long totalXP) → LevelInfo`.
- `Components/SkillStateComponent.cs` — the transient, in-memory per-player XP
  table. Never persisted and never replicated as a component.

Assets: `assets/skills/*.toml` (skill definitions) and `assets/lang/tags/*.toml`
(invariant gameplay tag lists, empty-language).

The `Name`/`Category` fields are **localization keys**, not display strings. The
client's `assets/lang/en/skills.toml` holds the display strings.

## Authority

- **Join seed**: the client sends its saved statistics on the `CharacterSeed`
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
progress bar; `notification.skill.levelUp` renders the level-up toast.

## Source of truth

- `WaywardBeyond.Shared.Skills/{Skills,Components}/*.cs`
- `WaywardBeyond.Shared.Skills/assets/{skills,lang/tags}/**`
- `WaywardBeyond.Server.Core/Systems/ServerSkillSystem.cs`
- `WaywardBeyond.Server.Core/Systems/ServerJoinSystem.cs` (seed)
- `WaywardBeyond.Client.Core/Systems/ClientNotificationSystem.cs`
- `WaywardBeyond.Client.Core/assets/lang/en/{skills,notification}.toml`
- `WaywardBeyond.Shared.Data/CodeGen/saves.nsd` (`Statistic`)

## Tests that pin this

- `Swordfish.Tests/SkillDatabaseTests.cs` — real tomls, tag expansion, data-id
  parity, level curve edges.
- `Swordfish.Tests/ServerSkillSystemTests.cs` — grant math and per-grant totals.
- `Swordfish.Tests/ServerJoinSkillSeedTests.cs` — join-time seeding.
- `WaywardBeyond.Client.Core.Tests/ClientNotificationSystemTests.cs` — key
  resolution, formatting, totals persistence.