# Improvement: Add keybinding rebinding

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

The settings page has no keybindings category. `ShortcutService` registers
shortcuts but has no capture mode, no way to enumerate current bindings, no
rebind path, and no persisted overrides. The settings page cannot show or edit
keybindings without a separate subsystem.

## Acceptance criteria

- [ ] `IShortcutService` exposes the registered shortcuts and their current
      bindings.
- [ ] The service can capture one key press without dispatching the captured
      shortcut.
- [ ] Rebinding updates the live service and persists to a config file.
- [ ] The keybindings category lists actions, current bindings, and conflicts.
- [ ] Reset restores the shipped binding for an action or for the whole
      category.
- [ ] The settings page handles a missing or conflicting binding without
      changing the live binding.
