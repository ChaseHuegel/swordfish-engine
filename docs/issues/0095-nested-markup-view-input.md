# Resolved: settings row controls did not render or receive clicks

## Status

Resolved. The cause was not nested views, scroll input, or hit-testing.

## Cause

`SettingsPage.RenderPage` assigned `_controlWidth` **after** building the row
states. Every control was therefore built with a fixed width of 0, rendered
nothing, and could not be clicked. Rows are now built after the layout width is
known.

## Regression coverage

- `Swordfish.Tests/SettingsInteractionTests.cs` clicks a choice cell, a stepper
  arrow, and a tray category, and asserts the draft and selection change.
- `Swordfish.Tests/ScrollInputReproTests.cs` keeps the isolated cases that ruled
  out scroll input: a plain element and the shared segmented control inside a
  scroll area, with and without the settings page's padded and clipped frame,
  plus markup equivalents in `Swordfish.Tests/Fixtures/ScrollReproView.rml`.

## Notes

The earlier suspicion of nested markup views was unfounded: every isolated
composition received clicks. When a migrated page's controls render but do not
respond, check the constraints first, then the hit test.
