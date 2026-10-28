# Settings UI

One subject: the settings page layout, controls, draft state, and apply
behavior.

## Layout

The page uses responsive constraints, not fixed child positions. `SettingsLayout`
provides the page size, the breakpoint, the tray width, and the label and control
column widths. The page builds the rest with rows, columns, fillers, and panels.

`SettingsLayout.Compute` is at
`WaywardBeyond.Client/UI/Settings/SettingsLayout.cs:30`. It produces:

- a centered page of at most 1280 by 840 pixels;
- a tray and a pane, or a horizontal tab strip and a full-width pane;
- a 48 pixel footer;
- label and control column widths for the current pane width.

The page width is `min(window width - 80, 1280)`. The page height is
`min(window height - 240, 840)`. The page leaves room for the title image in
`TitleMenu`.

The tray and pane show when the window is at least 900 pixels wide. Below
900 pixels, the tray becomes a tab strip above the pane. The tab strip sits
directly on the pane top and uses the short category names. The tray is a fixed list. A larger category set needs the
overflow work in
[settings category overflow](/docs/issues/0091-settings-tray-overflow.md).

The pane list scrolls with the mouse wheel through `Widgets.ScrollArea`. The
scrollbar is an indicator and does not accept dragging. The list clips its rows.

## Rows and controls

A category contains section headers, entries, and one reset row. The row height
is 44 pixels. A section header is 36 pixels. Each row is a horizontal layout.
The label takes the remaining width and the control uses the metric column.

The label uses `NoWrap` and clips to the row. The control column holds one of
these widgets. Each returns `ControlResult<T>`, so a row reads `.Changed` and
`.Value` instead of taking an `out` value:

- Category rows use the shared selected-row treatment: one orange border and a
  cyan left caret.
- `Widgets.Toggle` for on/off choices.
- `Widgets.SegmentedControl` for two or more radio-like choices.
- `Widgets.Stepper` for discrete values.
- `Widgets.Slider` for continuous values.
- `Widgets.ActionButton` for actions.
- The category reset row uses the danger button variant.

The widgets live in the game widget library. They use the shared theme and
metrics from [ui-theme](ui-theme.md). They use the Reef border, corner cap,
layout constraint, and no-wrap text support.

Disabled rows stay visible and dim. A setting can disable another row. For
example, V-Sync disables the frame limit, and Autosave disables the interval.

## Categories

`SettingsPage<TIdentifier>` owns the page shell: lifecycle, render order, apply,
dirty tracking, and reset. It takes its menu identifier as a constructor
argument, so one class serves both menus and the composition entry registers an
instance per identifier.

Each category is built by its own type in `WaywardBeyond.Client/UI/Settings/`:

- `DisplaySettingsCategory.Build` for display and video, including the
  resolution option list.
- `AudioSettingsCategory.Build` for the volume sliders.
- `GameplaySettingsCategory.Build` for autosave and HUD rows.
- `ControlSettingsCategory.Build` for mouse and placement rows.

A category takes the open `SettingsDraft` plus the localization it formats with,
so no builder dereferences the page.

## Draft state

`SettingsDraft` is at `WaywardBeyond.Client/UI/Settings/SettingsDraft.cs:11`.
It copies every editable config value when the page opens.

Each entry keeps its baseline value. The page compares the draft against the
baselines to decide whether changes exist. `Apply changes` commits the draft,
saves the configs, and captures new baselines. The footer action reads `Back`
when there are no pending changes and `Cancel` when there are. Both leave the
page, and `Cancel` discards the draft. `Reset section to defaults` restores one
category.

The recommended defaults are in `SettingsDefaults` at
`WaywardBeyond.Client/UI/Settings/SettingsDefaults.cs:7`. Display mode defaults
to `SettingsDisplayMode.BorderlessWindow`. Resolution defaults to the native
monitor resolution.

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
`WaywardBeyond.Client/UI/Layers/Menus/SettingsPage.cs:261` keeps the display
mode and resolution rows in sync when a shortcut or a manual resize changes the
window while the page is open.

## Real option set

The page exposes only options that the engine supports:

- Display & Video: display mode, resolution, V-sync, frame limit, field of
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
changes at `WaywardBeyond.Client/UI/Menu.cs:13` and `:134`.

`TitleMenu` gives a full-area page a full-height content host at
`WaywardBeyond.Client/UI/Layers/Menus/TitleMenu.cs:66`. Other pages keep the
existing spacer layout.

## Source

- Page: `WaywardBeyond.Client/UI/Layers/Menus/SettingsPage.cs:22`
- Categories: `WaywardBeyond.Client/UI/Settings/DisplaySettingsCategory.cs:12`,
  `AudioSettingsCategory.cs:7`, `GameplaySettingsCategory.cs:7`,
  `ControlSettingsCategory.cs:8`
- Layout: `WaywardBeyond.Client/UI/Settings/SettingsLayout.cs:6`
- Renderer: `WaywardBeyond.Client/UI/Settings/SettingsRenderer.cs:14`
- Draft: `WaywardBeyond.Client/UI/Settings/SettingsDraft.cs:11`
- Defaults: `WaywardBeyond.Client/UI/Settings/SettingsDefaults.cs:7`
- Theme: `WaywardBeyond.Client/UI/Theme/UiTheme.cs:6`
- Metrics: `WaywardBeyond.Client/UI/Theme/UiMetrics.cs:4`
- Widgets: `WaywardBeyond.Client/UI/Widgets/Widgets.Panel.cs:14`,
  `Widgets.ActionButton.cs:34`, `Widgets.Toggle.cs:16`,
  `Widgets.Stepper.cs:14`, `Widgets.Slider.cs:14`,
  `Widgets.Scrollbar.cs:13`
- Localization: `WaywardBeyond.Client/assets/lang/en/ui.toml`

## Tests

- `Swordfish.Tests/SettingsLayoutTests.cs` covers page fit, the 900 pixel
  breakpoint, display mode mapping, and resolution availability.
- `Swordfish.Tests/SettingsEntryTests.cs` covers dirty tracking, reset, and
  option lists.
- `Swordfish.Tests/SettingsPageRenderTests.cs` builds the real page and checks
  that the page centers and that controls stay inside the pane.
- `Swordfish.Tests/UiThemeResolverTests.cs` covers control styles and states.
