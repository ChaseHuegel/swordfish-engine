# Settings UI

One subject: the settings page layout, controls, draft state, and apply
behavior.

## Layout

The page uses fixed metrics. It does not rely on nested `Fill` or `Relative`
constraints inside the page.

`SettingsLayout.Compute` is at
`WaywardBeyond.Client/UI/Settings/SettingsLayout.cs:39`. It produces:

- a centered page of at most 1280 by 840 pixels;
- a 28 pixel title;
- a tray and a pane, or a horizontal tab strip and a full-width pane;
- a 48 pixel footer;
- a pane list rect and a scrollbar rect.

The page width is `min(window width - 80, 1280)`. The page height is
`min(window height - 240, 840)`. The page leaves room for the title image in
`TitleMenu`.

The tray and pane show when the window is at least 900 pixels wide. Below
900 pixels, the tray becomes a tab strip above the pane. The tab strip uses the
short category names. The tray is a fixed list. A larger category set needs
the overflow work in
[settings category overflow](/docs/issues/0091-settings-tray-overflow.md).

The pane list scrolls with the mouse wheel. The scrollbar is an indicator and
does not accept dragging. The list clips its rows.

## Rows and controls

A category contains section headers, entries, and one reset row. The row height
is 44 pixels. A section header is 36 pixels. The label column and control
column are fixed by `SettingsLayout`.

The label uses `NoWrap` and clips to the label column. The control column holds
one of these widgets:

- Category rows use the shared selected-row treatment: one orange border and a
  cyan left caret.
- `Widgets.Toggle` for on/off choices.
- `Widgets.SegmentedControl` for two or more radio-like choices.
- `Widgets.Stepper` for discrete values.
- `Widgets.Slider` for continuous values.
- `Widgets.ActionButton` for actions.

The widgets live in the game widget library. They use the shared theme and
metrics from [ui-theme](ui-theme.md). They use the Reef border, corner cap, and
no-wrap text support.

Disabled rows stay visible and dim. A setting can disable another row. For
example, V-Sync disables the frame limit, and Autosave disables the interval.

## Draft state

`SettingsDraft` is at `WaywardBeyond.Client/UI/Settings/SettingsDraft.cs:11`.
It copies every editable config value when the page opens.

Each entry keeps its baseline value. The page compares the draft against the
baselines to decide whether changes exist. `Apply Changes` commits the draft,
saves the configs, and captures new baselines. The footer action reads `Return`
when there are no pending changes and `Cancel` when there are. Both leave the
page, and `Cancel` discards the draft. `Reset section to defaults` restores one
category.

The recommended defaults are in `SettingsDefaults` at
`WaywardBeyond.Client/UI/Settings/SettingsDefaults.cs:7`. Display Mode defaults
to Borderless Window. Resolution defaults to the native monitor resolution.

## Display modes

`SettingsDisplayMode` is at
`WaywardBeyond.Client/UI/Settings/SettingsDisplayMode.cs:4`. The mapping methods
are in `SettingsDisplayModeExtensions.cs`. They cover the six real window
combinations:

| Setting | Window mode | Borderless |
|---|---|---|
| Windowed | Windowed | no |
| Borderless Window | Windowed | yes |
| Maximized | Maximized | no |
| Borderless Maximized | Maximized | yes |
| Fullscreen | Fullscreen | no |
| Borderless Fullscreen | Fullscreen | yes |

Resolution is editable only for Windowed and Borderless Window. The other modes
dim the resolution row and use the monitor resolution. See
[fullscreen resolution](/docs/issues/0089-fullscreen-resolution.md).

The page applies display changes through
`IWindowContext.ApplyDisplaySettings`. See [window-display](window-display.md).
`SettingsPage.RefreshExternalDisplaySettings` at
`WaywardBeyond.Client/UI/Layers/Menus/SettingsPage.cs:569` keeps the display
mode and resolution rows in sync when a shortcut or a manual resize changes the
window while the page is open.

## Real option set

The page exposes only options that the engine supports:

- Display & Video: display mode, resolution, V-Sync, frame limit, field of
  view, view distance, anti-aliasing, ambient occlusion, AO samples, bloom, and
  bloom passes.
- Audio: master, music, effects, and interface volume.
- Gameplay: autosave, autosave interval, control hints, crosshair, and name tag
  distance.
- Controls: mouse sensitivity, remember shape, and remember orientation.

The page does not expose FXAA, particle density, dynamic lighting, or an
environment audio channel. The engine has no such settings. View distance is
the camera far plane, so the page shows meters or kilometers, not chunks.

## Page shell

`IMenuPage.UsesFullContentArea` is at `WaywardBeyond.Client/UI/IMenuPage.cs:13`.
The interface also defines `OnEnter` and `OnExit`. `Menu` calls them on page
changes at `WaywardBeyond.Client/UI/Menu.cs:125` and `:134`.

`TitleMenu` gives a full-area page a full-height content host at
`WaywardBeyond.Client/UI/Layers/Menus/TitleMenu.cs:66`. Other pages keep the
existing spacer layout.

## Source

- Page: `WaywardBeyond.Client/UI/Layers/Menus/SettingsPage.cs:21`
- Layout: `WaywardBeyond.Client/UI/Settings/SettingsLayout.cs:7`
- Renderer: `WaywardBeyond.Client/UI/Settings/SettingsRenderer.cs:16`
- Draft: `WaywardBeyond.Client/UI/Settings/SettingsDraft.cs:11`
- Defaults: `WaywardBeyond.Client/UI/Settings/SettingsDefaults.cs:7`
- Theme: `WaywardBeyond.Client/UI/Theme/UiTheme.cs:6`
- Metrics: `WaywardBeyond.Client/UI/Theme/UiMetrics.cs:4`
- Widgets: `WaywardBeyond.Client/UI/Widgets/Widgets.Panel.cs:14`,
  `Widgets.ActionButton.cs:31`, `Widgets.Toggle.cs:16`,
  `Widgets.Stepper.cs:14`, `Widgets.Slider.cs:14`,
  `Widgets.Scrollbar.cs:13`
- Localization: `WaywardBeyond.Client/assets/lang/en/ui.toml`

## Tests

- `Swordfish.Tests/SettingsLayoutTests.cs` covers page geometry, the 900 pixel
  breakpoint, display mode mapping, and resolution availability.
- `Swordfish.Tests/SettingsEntryTests.cs` covers dirty tracking, reset, and
  option lists.
- `Swordfish.Tests/SettingsPageRenderTests.cs` builds the real page and checks
  that the page centers and that controls stay inside the list.
- `Swordfish.Tests/UiThemeResolverTests.cs` covers control styles and states.
