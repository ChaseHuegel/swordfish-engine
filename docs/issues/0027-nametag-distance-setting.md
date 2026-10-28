# Improvement: Name tag distance — default 32 and a settings-page control (0-64, step 8)

- Type: improvement
- Status: done
- Workflow: ../specs/issues.md

## Problem

Playtest: the default name tag render distance should be 32, and players
should be able to change it. Today the distance is a constant,
`NameplateSystem.MAX_RENDER_DISTANCE = 20` (`WaywardBeyond.Client/Systems/NameplateSystem.cs:17`), with
no user control.

## Acceptance criteria

- [x] `UISettings.NameplateDistance` (new `int` key, `ui.toml`, default
      32) replaces the `MAX_RENDER_DISTANCE` constant; `NameplateSystem`
      reads it per tick.
- [x] Settings page gains a name tag distance control following the
      existing `SliderEntry` precedent: range 0-64, increments of 8,
      0 = tags disabled.
- [x] Config key documented in `docs/specs/config-schemas.md` (docs
      pass).