# Resolved: settings row controls did not render or receive clicks

## Status

Resolved. The cause was not nested views or scroll input.

## Cause

`SettingsPage.RenderPage` assigned `_controlWidth` **after** building the row
states, so every control was built with a fixed width of 0 on the first frame
and rendered nothing. Clicks then had no target. Rows are now built after the
layout width is known, and `SettingsInteractionTests` covers a choice cell, a
stepper arrow, and the tray selection.

## Summary

When a generated view renders another generated view inside an element through
a delegate (`Body(ui)` invoked by a widget), controls in the inner view no
longer receive clicks. Hovering still works, and clicks on the outer view's own
elements work.

## Reproduction

1. Render `SettingsShellView` (`Body` draws `SettingsRowsView`).
2. Click a tray category (`Settings.Category.*`): it changes category, so the
   shell's own callback fires.
3. Click a pane row control (`Settings.VSync.1`, the segmented cell): the draft
   does not change, so `Settings.Apply` stays disabled.

The same control responds when it is rendered directly by the page, and the
segmented widget reports changes in a unit test with the same click sequence.

## Cause

Not yet isolated. Candidates:

- Hit registration for elements created inside a nested view is keyed by a
  scope the controller does not match, so `Clicked()` is false while
  `Hovering()` is true.
- The body delegate runs inside the shell's element scope, so element ids and
  interaction targets may be registered before the nested view's context.

## Workaround

Render the rows as a markup component rather than a nested view, so the rows
emit into the caller's scope.

## Repro suite (added)

`Swordfish.Tests/ScrollInputReproTests.cs` covers plain elements and the coded
segmented control inside a `ScrollArea`, with and without a padded, clipped
pane and page shell. All pass, so scroll input and widget input are not broken
in a page-shaped composition.

`Swordfish.Tests/Fixtures/ScrollReproView.rml` is the same case authored in
markup. The test project now runs the Reef generator (`Reef.Generator` analyzer
plus `Reef.Generator.props`). Both the plain element and the widget receive
clicks.

## Remaining problem

In the settings page, the choice and slider widgets inside the row list produce
no render commands at all, while labels, sections, and reset rows do render.
The generated code calls `SettingsChoiceWidget.SettingsChoice` and
`SettingsSliderWidget.SettingsSlider` with per-row constraints and the view's
delegate property, so the call is reached. Next step: instrument those two
wrappers or diff their generated call against the passing repro widget call.

## Earlier findings from the component attempt

- A component cannot reference an external widget namespace. Declaring
  `xmlns:s="WaywardBeyond.Client.UI.Settings"` on the component root and using
  `<s:SettingsChoice ... />` reports `RF0007` (the prefix is not declared) even
  though the declaration sits on the Reef root. Views do not have this problem.
  This blocks a rows component that wraps the coded choice or slider widgets.
- Inlining the rows into the page view (no nested view) was not enough to make
  the controls respond in the page-level probe. The tray still worked. The
  probes that followed were inconclusive because the rows stopped rendering
  after ad-hoc layout edits, so no trustworthy conclusion was reached.

## Verification

Re-adding a shell-level click test and observing a draft change (Apply enabled)
is the acceptance check.
