# Bug: interactive elements in a nested markup view do not receive clicks

## Status

Open. Blocks moving the settings rows to markup.

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

## Findings from the component attempt

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
