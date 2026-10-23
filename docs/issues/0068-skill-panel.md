# Improvement: Add a skill panel that consumes skill icons and categories

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

`WaywardBeyond.Skills/Skill.cs` carries `Icon` (a texture path) and `Category`
(a localization key) on every loaded skill, and nothing reads either field. The
client renders skill notifications only, so a player cannot see their totals,
their level, or the curve.

The fields are a leftover of the client-owned model. The base branch resolved
the icon through the texture database and fell back to `skills/unknown.png`
with a logged error. The loader moved to the shared headless database, so icon
paths are now neither loaded nor validated.
`WaywardBeyond.Client/assets/textures/skills/unknown.png` is unreferenced.

## Acceptance criteria

- [ ] A panel lists every skill from `ISkillDatabase` with its name, category,
      level, total XP, and XP into the level.
- [ ] The panel groups the list by `Category`.
- [ ] The panel resolves `Skill.Icon` through the texture database, falls back
      to `skills/unknown.png`, and logs a warning on an unknown path, matching
      `ItemDatabase`.
- [ ] `assets/skills/*.toml` point `Icon` at skill artwork, not at item or
      block texture paths.
- [ ] A test pins the level and bar math against `Skill.CalculateLevel`.
- [ ] The skills spec and the asset definition table describe the icon fallback.
