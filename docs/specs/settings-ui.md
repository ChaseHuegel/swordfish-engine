# Settings UI

One subject: the settings page layout, controls, draft state, and apply
behavior.

## Layout

The whole page is markup. `SettingsShellView.rml` owns the page box, the tray,
the tab strip, the pane frame, the scrolling row list, and the footer, and
`SettingsShellState` carries the category labels, selection, title, dirty flag,
and status. `SettingsRowState` is the per-row model, produced by
`SettingsRow.Snapshot` on the row types.

Row chrome, labels, sections, choices, sliders, steppers, keybindings, and
reset rows are markup. `SettingsRows.rml` renders every row branch from
`SettingsRowState`.

Each control reports its row id through a callback on `SettingsRowsState`, so
the page updates the draft and plays the interface sound. The component owns its
controls and the page owns the draft.

The page uses responsive constraints, not fixed child positions.

All page geometry comes from markup constraints. The page is `window - 80` wide
and `window - 240` tall with 320/280 floors and 1280/840 caps. The tray is `24%`
wide with a 220 pixel floor and a 300 pixel cap. The breakpoint is
`ui.Width < 900`. The control column is `33.334%` of the row with a 220 pixel
floor and a 340 pixel cap; the label takes the rest.

The control column no longer shrinks to protect a 120 pixel label on very
narrow panes; below roughly 356 pixels of list width the label is squeezed
instead.

The page width is `min(window width - 80, 1280)`. The page height is
`min(window height - 240, 840)`. The page leaves room for the title image in
`TitleMenu`.

The tray and pane show when the window is at least 900 pixels wide. Below
900 pixels, the tray becomes a tab strip above the pane. The tab strip sits
directly on the pane top and uses the short category names. The tray is a fixed list. A larger category set needs the
overflow work in
[settings category overflow](/docs/issues/0091-settings-tray-overflow.md).

The pane list scrolls with the mouse wheel through the `ScrollArea` markup
component. The scrollbar thumb drags, and a track click jumps toward the click.
A drag keeps its hold after the cursor leaves the rail, and a release ends it.
The list clips its rows. Selecting another category resets the list to the top,
and reopening the page starts at the top. `SettingsPage` calls
`ScrollAreaState.Reset` in `OnEnter` and `OnCategorySelected`.

## Rows and controls

A category contains section headers, entries, and one reset row. Markup gives a
row a height of 44 pixels and a section header 35 pixels. Each row is a
horizontal layout. The label takes the remaining width and the control uses the
control column.

The label uses `NoWrap` and clips to the row. The stepper value uses `NoWrap`
and clips to the space between the arrows. A long value does not wrap into the
next row or cover the arrows. The control column holds one of these controls:

- Category rows use the shared selected-row treatment: one orange border and a
  cyan left caret.
- A toggle or radio-like choice uses the `SegmentedControl` component.
- A continuous value uses the `Slider` component. It reports a step direction
  from its arrows and a normalized track position from a drag.
- The shared `Stepper` component covers discrete values. It reads its options
  from a provider on each snapshot. A static list is a provider that returns the
  same list.
- A keybinding row displays a `.bind` capture button, a clear button (`x`), and
  a per-action reset button inside the control column. The `.bind` button highlights
  cyan while listening, or danger red when unbound or in conflict.
- The category reset row uses the danger button variant.

The slider arrows step by `SliderEntry.StepSize`. A track drag snaps to the same
step. The page plays an increase or decrease cue only when the value changes, so
a held drag does not loop the cue. Colors, fonts, spacing, and variants live in
the theme and component styles. See [ui-theme](ui-theme.md).

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
- `AudioSettingsCategory.Build` for the output device row and the volume sliders.
- `GameplaySettingsCategory.Build` for autosave and HUD rows.
- `ControlSettingsCategory.Build` for mouse and placement rows.
- `KeybindingsSettingsCategory.Build` for rebindable movement, interaction,
  inventory, general, and developer shortcuts.

A category takes the open `SettingsDraft` plus the localization it formats with,
or `IShortcutService` for keybindings, so no builder dereferences the page.

## Draft state

`SettingsDraft` is at `WaywardBeyond.Client/UI/Settings/SettingsDraft.cs:13`.
It copies every editable config value when the page opens.

The client applies its declared render defaults at composition, before any
page reads the render config. `SettingsDefaults.ApplyRenderDefaults` at
`WaywardBeyond.Client/UI/Settings/SettingsDefaults.cs:80` sets the bloom
quality and particle density when the config has no saved file. A saved
`render.toml` keeps the user's values. The snapshot fixture composes a fresh
render config through the same method.

Each entry keeps its baseline value. The page compares the draft against the
baselines to decide whether changes exist. `Apply changes` commits the draft,
saves the configs, and captures new baselines. The footer action reads `Back`
when there are no pending changes and `Cancel` when there are. Both leave the
page, and `Cancel` discards the draft. `Reset section to defaults` restores one
category.

The recommended defaults are in `SettingsDefaults` at
`WaywardBeyond.Client/UI/Settings/SettingsDefaults.cs:7`. Display mode defaults
to `SettingsDisplayMode.BorderlessWindow`. Resolution defaults to the native
monitor resolution. The output device defaults to the empty string, which means
`System default`.

The output device preference is `VolumeSettings.OutputDevice` in
`volume.toml`. `ConfigManager` applies it to the engine at startup. The page
writes the draft value to the config and to `IPlaybackDeviceService` on apply.
Cancel leaves both unchanged.

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

Resolution is editable for Windowed, Borderless Window, and Fullscreen.
Maximized, Borderless Maximized, and Borderless Fullscreen dim the resolution
row and use the monitor resolution.

The page applies display changes through
`IWindowContext.ApplyDisplaySettings`. See [window-display](window-display.md).
The `Settings.Monitor` stepper is editable only in Fullscreen mode. It selects
`Auto` (empty string) or any connected monitor name from
`IWindowContext.GetMonitors`, keeping an unavailable saved monitor visible. When
an unsupported fullscreen resolution falls back to a supported mode,
`SettingsPage.ApplySettings` syncs the draft to the applied mode and shows
`ui.settings.status.displayFallback`.
`SettingsPage.RefreshExternalDisplaySettings` at
`WaywardBeyond.Client/UI/Layers/Menus/SettingsPage.cs:738` keeps the display
mode, monitor, and resolution rows in sync when a shortcut or a manual resize
changes the window while the page is open.

## Keybindings

`KeybindingsSettingsCategory.Build` reads all rebindable shortcuts from
`IShortcutService.GetShortcuts()` and groups them into `Movement`,
`Interaction`, `Inventory`, `General`, and `Developer` sections. Shared
`General/Back` (`Back / close`, default `Esc`, `Global` scope) unifies pause
toggle, menu navigation back, modal close, inventory close, and chat close, so
players can rebind or clear it while `Esc` still cancels active key capture.
Hotbar and inventory share the `Inventory/Slot 1` through `Inventory/Slot 9`
bindings, and the `Inventory` section also exposes rebindable `Move / stack`
(`LMB`), `Split / distribute` (`RMB`), and `Quick move` (`Shift+LMB`) actions.
The `Developer` section is shown only in `DEBUG` builds or when launched with
`-debug` or `--debug` (`CommandLineArgs.GetFlag("debug")`).

Clicking a `.bind` button starts `IShortcutService.BeginCapture` without
dispatching game shortcuts. `Esc` cancels capture cleanly without leaving the
page. Capturing a mouse button suppresses UI clicks for two frames so binding
`LMB` does not immediately re-enter capture.

`KeybindingsSettingsCategory.UpdateConflicts` validates the draft across all
active scopes (`KeybindingScope.cs:4`) and reserved fixed bindings on every
frame. World-only shortcuts (`Movement`, `Interaction`, and `Open chat`),
modal inventory actions (`Move / stack`, `Split / distribute`, and
`Quick move`), and `Send chat` (`Chat` scope) live in separate scopes so
default bindings (`LMB`, `RMB`, `Enter`) do not false-conflict. Global shortcuts
(including `Inventory`, `Slot 1` through `Slot 9`, `General`, and `Developer`)
overlap every scope. Two conflicting entries in overlapping
scopes highlight in danger red unless both share the same `SharedBindingGroup`,
such as the `ToolRadial` group for tool radials on `R`. A rebindable entry
matching a `Fixed` shortcut binding also highlights in danger red. Cleared rows
show `Unbound` in danger red and do not conflict with each other or with fixed bindings.
Only conflicts or active capture disable and block `Apply changes`, leaving
live `IShortcutService` bindings and `keybindings.toml` untouched until resolved.
Users can clear any rebindable action, leave it unbound, and save it as
`"Unbound"` in `keybindings.toml`. Each row supports clear and per-action reset,
and the category reset restores all keybindings to their shipped defaults.

When applied, `SettingsPage.ApplySettings` commits dirty rows via
`IShortcutService.Rebind`, syncs `KeybindingSettings.SetOverrides`, and saves
`keybindings.toml`. Rebinding fires `IShortcutService.BindingsChanged`, which
updates all 13 live keycap badges in `ControlHints`
(`WaywardBeyond.Client/UI/Layers/ControlHints.cs:18`), `Hotbar`, `Inventory`,
and both `ChatLayer` close and send bar hints
(`WaywardBeyond.Client/UI/Layers/ChatLayer.cs:29`).

## Real option set

The page exposes only options that the engine supports:

- Display & Video: display mode, monitor, resolution, UI scale, V-sync, frame
  limit, field of view, view distance, anti-aliasing, ambient occlusion, AO
  quality, bloom, bloom quality, and particle density.
- Audio: output device, master, music, effects, and interface volume.
- Gameplay: autosave, autosave interval, control hints, crosshair, and name tag
  distance.
- Controls: mouse sensitivity, remember shape, and remember orientation.
- Keybindings: movement keys, interaction and tool actions, hotbar/inventory
  slots 1-9, chat and general UI shortcuts, and developer debug toggles.

The page does not expose FXAA, dynamic lighting, or an environment audio
channel. The engine has no such settings. View distance is the camera far
plane, so the page shows meters or kilometers, not chunks.

## Audio output device

The audio category starts with an `Output` section. The section contains one
`Settings.OutputDevice` stepper:

- The first option is `System default` and uses the empty string.
- The remaining options are `IPlaybackDeviceService.PlaybackDeviceNames` in
  ordinal order.
- The options provider appends the current draft value when the device list no
  longer contains it. The row keeps the missing selection visible.
- A missing selection formats as `Name (unavailable)`. This includes a device
  that failed initialization.
- A long device name clips to the space between the stepper arrows. It does not
  wrap into the next row or cover the arrows.
- A missing selection does not change the saved preference. The engine falls
  back to the system default, and the device resumes when it returns.

The row has no dropdown. The shared stepper exposes the list by cycling. This is
the same control that the resolution row uses. A list or popup control is a
separate UI feature.

`SettingsPage` subscribes to `IPlaybackDeviceService.PlaybackDevicesChanged` in
`OnEnter` and unsubscribes in `OnExit`. The handler runs on the ECS thread and
sets a flag. `RenderPage` consumes the flag, marks the rows dirty, and keeps the
draft preference when the entry is not dirty. The stepper reads the options
provider on the next snapshot, so the row shows the new device list within one
frame of the event.

The scan interval is 2 seconds. A hotplug reaches the row on the next scan.

## Page shell

`IMenuPage.UsesFullContentArea` is at `WaywardBeyond.Client/UI/IMenuPage.cs:13`.
The interface also defines `OnEnter` and `OnExit`. `Menu` calls them on page
changes at `WaywardBeyond.Client/UI/Menu.cs:13` and `:134`.

`TitleMenu` gives a full-area page a full-height content host at
`WaywardBeyond.Client/UI/Layers/Menus/TitleMenu.cs:66`. Other pages keep the
existing spacer layout.

## Source

- Page: `WaywardBeyond.Client/UI/Layers/Menus/SettingsPage.cs:20`
- Categories: `WaywardBeyond.Client/UI/Settings/DisplaySettingsCategory.cs:30`,
  `AudioSettingsCategory.cs:11`, `GameplaySettingsCategory.cs:9`,
  `ControlSettingsCategory.cs:10`, `KeybindingsSettingsCategory.cs:36`
- Audio output row: `WaywardBeyond.Client/UI/Settings/AudioSettingsCategory.cs:24`,
  `:71`, `:96`
- Audio refresh and apply: `WaywardBeyond.Client/UI/Layers/Menus/SettingsPage.cs:81`,
  `:146`, `:579`, `:677`, `:715`
- Keybinding entry and shortcuts: `WaywardBeyond.Client/UI/Settings/KeybindingEntry.cs:7`,
  `WaywardBeyond.Client/UI/Settings/KeybindingScope.cs:4`,
  `WaywardBeyond.Client/Shortcuts/GameShortcuts.cs:7`
- Keybinding persistence and hints: `WaywardBeyond.Client/Configuration/KeybindingSettings.cs:10`,
  `WaywardBeyond.Client/Configuration/ConfigManager.cs:62`, `:74`,
  `WaywardBeyond.Client/UI/Layers/ControlHints.cs:18`
- Particle density preset: `WaywardBeyond.Client/UI/Settings/ParticleDensity.cs:4`,
  `ParticleDensityExtensions.cs:7`
- Audio preference: `WaywardBeyond.Client/Configuration/VolumeSettings.cs:14`,
  `WaywardBeyond.Client/Configuration/ConfigManager.cs:63`
- Draft: `WaywardBeyond.Client/UI/Settings/SettingsDraft.cs:13`
- Defaults: `WaywardBeyond.Client/UI/Settings/SettingsDefaults.cs:7`
- Stepper entry: `WaywardBeyond.Client/UI/Settings/StepperEntryOfT.cs:7`
- Tokens: `WaywardBeyond.Client/UI/Theme/Theme.rss:1`
- Classes: `WaywardBeyond.Client/UI/Components/Components.rss:1`
- Choice component: `WaywardBeyond.Client/UI/Components/SegmentedControl.rml:1`,
  `SegmentedControlParameters.cs:6`
- Slider component: `WaywardBeyond.Client/UI/Components/Slider.rml:1`,
  `SliderParameters.cs:7`
- Slider entry: `WaywardBeyond.Client/UI/Settings/SliderEntry.cs:6`
- Stepper component: `WaywardBeyond.Client/UI/Components/Stepper.rml:1`,
  `StepperParameters.cs:8`
- Localization: `WaywardBeyond.Client/assets/lang/en/ui.toml`

## Tests

- `WaywardBeyond.Tests/SettingsPageRenderTests.cs` builds the real page and checks
  that the page centers, that controls stay inside the pane, and that labels
  and slider buttons center vertically.
- `WaywardBeyond.Tests/SettingsShellViewTests.cs` covers the shell layout
  snapshots across wide, medium, and tab breakpoints.
- `WaywardBeyond.Tests/SettingsInteractionTests.cs` covers click, scroll,
  slider button, playback device refresh, display fallback, callback behavior,
  and the row scroll reset on category switch and page reopen.
- `WaywardBeyond.Tests/InventoryViewTests.cs` covers inventory layout snapshots,
  stack drag and distribution, scroll transfers, and rebound inventory actions.
- `WaywardBeyond.Tests/KeybindingSettingsTests.cs` covers TOML override round
  trips including saved `"Unbound"` entries, malformed override skipping,
  `SetOverrides`, and `ApplyOverrides`.
- `WaywardBeyond.Tests/KeybindingSettingsInteractionTests.cs` covers category
  sections, single-input capture, Esc cancel without triggering `Back / close`,
  mouse capture click suppression, modal inventory and chat scope isolation,
  global and fixed-binding conflicts, tool radial shared groups, saving unbound
  actions, developer section gating via `-debug` and `--debug`, per-action and
  category reset, and apply.
- `WaywardBeyond.Tests/ChatLayerTests.cs` covers `Open Chat`, `Send Chat`, and
  `Back / close` shortcut execution, live close and send hint formatting,
  unbound handling, and same-frame message submission.
- `WaywardBeyond.Tests/ControlHintsTests.cs` covers all 13 control hints,
  late override registration refreshes, and live keycap updates when bindings
  change.
- `WaywardBeyond.Tests/HotbarInventoryLinkedBindingTests.cs` covers shared slot
  1-9 rebinding between the hotbar and inventory.
- `WaywardBeyond.Tests/ScrollBarInteractionTests.cs` covers thumb drag, drag outside
  the rail, track jump, release, and end clamping.
- `WaywardBeyond.Tests/SettingsControlTests.cs` covers the coded choice and
  slider controls.
- `WaywardBeyond.Tests/SettingsEntryTests.cs` covers dirty tracking, reset, slider
  steps and drag changes, dynamic option providers, and the bloom, shadow, AO,
  and particle density preset round trips.
- `WaywardBeyond.Tests/AudioSettingsCategoryTests.cs` covers the output device
  option order, the missing-device label, and reset.
- `WaywardBeyond.Tests/DisplaySettingsTests.cs` covers fullscreen video mode
  resolution, fallback, resolution row enablement, and persisted fullscreen
  resolution loading.
