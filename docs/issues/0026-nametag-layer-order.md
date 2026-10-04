# Bug: Name tag layer renders over the HUD

- Type: bug
- Status: open
- Workflow: ../specs/issues.md

## Problem

Playtest: name tags render above HUD elements like the hotbar and
inventory. NameplateSystem (`NameplateSystem.cs`) draws on a rendering
layer ordered over the game UI, so player name tags (world-space) overlay
the hotbar/inventory widgets instead of being occluded by them.

## Acceptance criteria

- [ ] Name tag rendering is ordered below HUD layers (hotbar, inventory,
      notifications) in the render/layer stack.
- [ ] Visual verification: with another player's tag visible, opening the
      hotbar and inventory never draws the tag over the widgets.
- [ ] The layer-ordering constants are documented where the render stack
      is defined (docs pass if a spec names the layer order).