# Improvement: Show the menu spaceship

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

The main menu was designed around a voxel spaceship that turns behind the menu
buttons. The blueprint load and entity spawn are not wired up, so the menu
renders over an empty background.

## Acceptance criteria

- [ ] The main menu loads a background blueprint and spawns it as a voxel
      entity behind the menu.
- [ ] A failed load logs one error and leaves the menu usable.
- [ ] The entity is removed when the main menu is left.
