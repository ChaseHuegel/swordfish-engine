# Server-Authoritative Skills + Remote Notifications (implementation plan)

Goal: skills become server-authoritative. Clients seed their skill data at join and then act as a
dumb display; the server grants XP, computes levels, and pushes notifications. Notifications become
invokable from both client and server; servers send localization keys, clients format.

**Decisions locked with the user:**
- New `WaywardBeyond.Shared.Skills` module owns skill models, headless loader, and tomls.
- Two separate wire messages: `NotificationMessage` (generic display) + `SkillStateUpdateMessage`
  (authoritative totals).
- Server holds a transient in-memory `SkillStateComponent` per player (seeded at join, freed with
  the entity, never persisted).
- Skill `Sources` expand to brick dataIDs via `FNV1a.ComputeDataID` in shared code; the server
  matches `Voxel.ID`.
- `NotificationMessage.ID`/`Amount` are nullable (Bar-only fields).

All work is `[S]` (shared) or `[G]` (game); no engine (`[E]`) changes are required. Commit
boundaries respect the engine-first / standalone / shared-code rules.

---

## M1 — Shared skills module `[S]`

New `WaywardBeyond.Shared.Skills` module (net9.0, manifest ID `waywardbeyond.shared.skills`):
- Move schema: `XPSource`, `SkillDefinition`, `SkillDefinitions` (TOML schema keeps string/tag keys).
- `SkillData` runtime (ID, NameKey, CategoryKey, IconPath, MaxLevel, sorted Levels, dataID-keyed
  `Sources`) produced by a headless `SkillDatabase : VirtualAssetDatabase<...>` (needs only
  `Swordfish.Library` + `IFileParseService`/VFS). Expands tags -> brick IDs -> dataIDs via
  `FNV1a.ComputeDataID`, warns on source-set dataID collisions.
- `SkillDataExtensions.CalculateLevel` + `LevelInfo`.
- `SkillStateComponent` (plain struct holding `Dictionary<string, long>`; server-only, not in
  `NetworkRegistry`).
- `Injector : IDryIocInjector`: `RegisterTomlParser<SkillDefinitions>()`, `SkillDatabase`,
  `IAssetDatabase<SkillData>`.
- Assets moved from client: `assets/skills/{mining,building,salvaging}.toml`,
  `assets/lang/tags/{buildable,environment,manufactured}.toml`.
- Wiring: `ProjectReference ModulePath="waywardbeyond.shared.skills"` in
  `WaywardBeyond.Client.Launcher.csproj` + LoadOrder entry in its `config/modules.toml`; add to
  `Swordfish.Launcher` too. Client code untouched.
- Tests (`Swordfish.Tests`, fixture tomls): parse, tag -> dataID expansion + FNV1a parity,
  `CalculateLevel` edges, collision warning.

## M2 — Server skill authority `[S]`/`[G]`

- `world.nsd`: add `CharacterSeed.Statistics`.
- `network.nsd`: add `NotificationMessage` and `SkillStateUpdateMessage`; register serializers.
- `SessionManager.TryGetClient(entity, out Uuid clientId)` reverse map.
- `ServerJoinSystem.SeedInteractionContext`: seed `SkillStateComponent` from `seed.Statistics`.
- `ServerSkillSystem.OnInteractionApplied`: accumulate into the component, compute prev/current
  level, send `SkillStateUpdateMessage` to that player's client.
- `ServerContext` injects it into `ServerInteractionSystem`, called in `Break`/`Place` cases using
  `resolution.Voxel.ID` (Break captured before `Set(new Voxel())`).
- `ClientJoinSystem`: send `Statistics = character.Statistics` in the seed.

## M3 — Client cutover `[G]` (atomic)

- New `ClientNotificationSystem`: drains `NotificationMessage` (resolve args -> localize -> push;
  Bar ctor only when `ID`/`Amount` non-null) and `SkillStateUpdateMessage` (persist totals).
- `ServerSkillSystem` also sends `NotificationMessage` (Bar per grant, Toast on level-up).
- Talent templates -> positional: `notification.skill.bar` = `"{0} - {1}"`,
  `notification.skill.levelUp` = `"{0} increased from {1} to {2}!"`.
- Delete the client skill pipeline: `Skills/*`, `Skills/Listeners/*`, `Events/{XPEvent,LevelUpEvent}`,
  event invoker registrations, parser/asset registrations, icon assets, moved toml copies.

## M4 — Tests + docs pass

- `dotnet build`, `dotnet test Swordfish.Tests`, `dotnet test WaywardBeyond.Client.Core.Tests`.
- Docs: `asset-definitions.md` (skills -> shared), `networking-messages.md`, `networking-join.md`,
  `persistence.md`, new `docs/specs/skills.md` + `docs/README.md` row, module map in
  `docs/architecture.md`, repo index in `AGENTS.md`.

## Commit order

1. M1 — shared skills module + launcher wiring — `[S]`
2. M2 — server authority (nsd, seeding, `ServerSkillSystem`) — `[S]`/`[G]`
3. M3 — client cutover (notification system + deletions) — `[G]`
4. M4 — tests + docs pass — `[S]`/`[G]`

## Risks / notes

- DataID parity vs the client brick collision pass: a colliding placed brick could mismap XP.
  Mitigated by warn-on-collision in the shared loader; a future canonical brick-id registry removes it.
- Reliable transport (TCP/local, ordered) keeps `TotalXP` exact; no drift.
- Bar `Amount` at over-max keeps the existing `nextLevelXP = 1` fallback parity.