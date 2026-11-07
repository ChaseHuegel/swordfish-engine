# Improvement: Make the settings scrollbar draggable

- Type: improvement
- Status: done
- Workflow: ../specs/issues.md

## Problem

The settings pane scrollbar shows the scroll position but does not accept
input. The mouse wheel is the only way to scroll a long category.

## Acceptance criteria

- [x] Dragging the scrollbar thumb scrolls the pane.
- [x] Clicking the scrollbar track jumps toward the clicked position.
- [x] The wheel still scrolls the pane.
- [x] The thumb stays inside the track at both ends.

## Notes

`ScrollBarState` owns the thumb geometry and the grab offset.
`ScrollAreaState` maps thumb progress to the scroll offset. The scrollbar
overlay uses the existing `onclick` and `onheld` input. Reef keeps the active
pointer bound to the pressed element, so the drag continues outside the rail
and stops when the left button is released.
