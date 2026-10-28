# Improvement: Handle settings category overflow

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

The settings tray is a fixed vertical list. It fits the current four categories,
and roughly eight categories at 720p. A larger category set would draw items
below the tray panel, where they are clipped and cannot be selected.

## Acceptance criteria

- [ ] The tray scrolls, or it switches to another layout, when the categories
      do not fit.
- [ ] Every category stays reachable at 720p and above.
- [ ] The wheel over the tray scrolls the tray without scrolling the pane.
- [ ] The selected category stays visible when the tray scrolls.
