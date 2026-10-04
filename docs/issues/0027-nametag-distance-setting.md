# Improvement: Name tag distance — default 32 and a settings-page control (0-64, step 8)

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

Playtest: the default name tag render distance should be 32, and players
should be able to change it. Today the distance is a constant,
`NameplateSystem.MAX_RENDER_DISTANCE = 20` (`NameplateSystem.cs:22`), with
no user control.

## Acceptance criteria

- [ ] `UISettings.NameplateDistance` (new `int` key, `ui.toml`, default
      32) replaces the `MAX_RENDER_DISTANCE` constant; `NameplateSystem`
      reads it per tick.
- [ ] Settings page gains a name tag distance control following the
      existing `RenderDistance` `NumberControl` precedent
      (`SettingsPage.cs:277-284`): range 0-64, increments of 8,
      0 = tags disabled.
- [ ] Config key documented in `docs/specs/config-schemas.md` (docs
      pass).