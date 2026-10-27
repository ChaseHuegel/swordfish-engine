# Bug: Settings row controls did not render or receive clicks

- Type: bug
- Status: done
- Workflow: ../specs/issues.md

## Problem

The settings page built the row controls before it assigned the control width.
Every control had a fixed width of zero, rendered nothing, and could not be
clicked.

## Acceptance criteria

- [x] The settings page assigns the control width before it builds the row
  states.
- [x] `SettingsInteractionTests` clicks a choice cell, a stepper arrow, and a
  tray category, and asserts that the draft and the selection change.
- [x] The isolated scroll cases in `ScrollInputReproTests` show that scroll
  input and nested views are not the cause.

## Notes

The earlier suspicion of nested markup views was unfounded. When a migrated page
renders controls that do not respond, check the constraints first, then the hit
test.
