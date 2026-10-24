# Improvement: Add keyboard and gamepad menu navigation

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

The settings page and the other menus are mouse-only. `UIController` tracks
hover and clicks but has no focus model, no focus ring, and no directional
navigation. A gamepad or keyboard-only player cannot use the settings page.

## Acceptance criteria

- [ ] A page can nominate focusable elements in visual order.
- [ ] Arrow keys, WASD, or a gamepad stick move focus.
- [ ] Enter, Space, or the primary gamepad button activates the focused item.
- [ ] Escape or the secondary gamepad button goes back.
- [ ] The focused item has a visible focus ring.
- [ ] The settings list scrolls to keep the focused row visible.
