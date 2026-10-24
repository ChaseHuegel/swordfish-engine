# Improvement: Switch fullscreen resolution

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

The settings page disables the resolution row in Maximized, Borderless
Maximized, Fullscreen, and Borderless Fullscreen. The engine lists video modes,
but `SilkWindowContext` has no verified path for changing the exclusive
fullscreen video mode. The page shows `Native` for those modes.

## Acceptance criteria

- [ ] The display contract can apply a supported fullscreen video mode.
- [ ] The settings page enables the resolution row in fullscreen modes.
- [ ] An unsupported mode falls back to a supported mode and reports the
      failure without leaving the window in an invalid state.
- [ ] The selected mode persists across a restart.
