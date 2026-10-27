# Improvement: Add a window concept to the UI layers

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

Interaction blocking is a single global flag, so it cannot express "this layer
blocks the game but not that layer". Three symptoms follow.

- The pause shortcut has to special-case the playing state to avoid pausing
  while the inventory closes.
- The shape and orientation selectors block interaction, so they also block
  pausing, which is wrong.
- `ModalMenu` and other layers manage their blocker from `IsVisible()`, which
  happens to be polled once per frame. The blocker state therefore depends on
  render scheduling.

## Acceptance criteria

- [ ] A layer declares whether it blocks game input, pause input, or both.
- [ ] ESC closes the topmost closable window before it pauses the game.
- [ ] Opening a modal acquires and releases its blocker without relying on
      `IsVisible()` being polled.
- [ ] The pause shortcut needs no per-state `when` special case.
