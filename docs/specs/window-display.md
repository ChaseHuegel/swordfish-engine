# Window display

One subject: the `IWindowContext` display contract for video modes and batched
display changes.

## Contract

`Swordfish/Graphics/IWindowContext.cs:27` exposes `GetVideoModes`. It returns
the distinct fullscreen resolutions that the current monitor reports.

`Swordfish/Graphics/IWindowContext.cs:34` exposes `ApplyDisplaySettings`. It
applies a `WindowMode`, a borderless flag, and an optional resolution as one
update and returns a `Result`. A null size keeps the saved size for the target
mode.

`Swordfish/Settings/WindowSettings.cs:16` stores `FullscreenWidth` and
`FullscreenHeight` separately from the windowed `Width` and `Height` bindings so
exclusive fullscreen modes persist across restarts without overwriting the
windowed size.

## Behavior

`SilkWindowContext.GetVideoModes` is at
`Swordfish/Graphics/SilkWindowContext.cs:168`. It reads
`Window.Monitor.GetAllVideoModes`, drops null and duplicate resolutions, sorts
by width then height, and falls back to the current window size.

`SilkWindowContext.ResolveFullscreenVideoMode` is at
`Swordfish/Graphics/SilkWindowContext.cs:220`. It matches a requested exclusive
fullscreen resolution against the monitor's supported video modes, preferring
the monitor's current refresh rate and otherwise the highest available refresh
rate. If the requested resolution is unsupported, it selects a supported
fallback mode and returns a failure `Result`.

`SilkWindowContext.ApplyDisplaySettings` is at
`Swordfish/Graphics/SilkWindowContext.cs:276`. For exclusive fullscreen
(`WindowMode.Fullscreen` without borderless), it delegates to
`ApplyExclusiveFullscreenSettings` at
`Swordfish/Graphics/SilkWindowContext.cs:336`, resolves the video mode, persists
`FullscreenWidth` and `FullscreenHeight`, and applies the monitor video mode
through GLFW in `ApplyExclusiveFullscreen` at
`Swordfish/Graphics/SilkWindowContext.cs:784`. On startup, `SilkWindowContext`
restores saved exclusive fullscreen settings through the same path at
`Swordfish/Graphics/SilkWindowContext.cs:100`.

For windowed, maximized, and borderless fullscreen modes,
`ApplyDisplaySettings` sets the borderless and mode bindings while
`_suppressWindowEvents` is true and applies the window style once. When the
target mode is `Windowed`, it then clamps the requested size to the restored
monitor bounds, persists `Width` and `Height`, applies the size, and centers the
window. The position is saved with the same suppression.

The size must be applied after the window style. Silk.NET restores the saved
non-fullscreen size and desktop video mode when the window leaves fullscreen.
Clamping or applying the windowed size first therefore clamps against the old
fullscreen video mode or loses the requested size.

The suppression checks are in the X, Y, width, height, fullscreen width,
fullscreen height, borderless, allow-resize, and mode change handlers at
`Swordfish/Graphics/SilkWindowContext.cs:619`, `:629`, `:639`, `:649`, `:659`,
`:669`, `:679`, `:697`, and `:715`. This prevents a display change from resizing
or moving the window several times.

The method logs the requested and actual window state at
`Swordfish/Graphics/SilkWindowContext.cs:320` and `:401`. Use those lines to
diagnose a platform that leaves a mixed state.

## Shortcuts

`ToggleMaximized` at `Swordfish/Graphics/SilkWindowContext.cs:525` switches
between `Maximized` and `Windowed`. It is bound to F11.

`ToggleFullscreen` at `Swordfish/Graphics/SilkWindowContext.cs:535` switches
between `Fullscreen` and `Maximized`. It is bound to Alt+Enter. Both call
`ApplyDisplaySettings` without a size, so the windowed and fullscreen sizes are
preserved.

The key match rule is in [input-shortcuts](input-shortcuts.md). A held
modifier blocks a shortcut that declares none. For example, Shift+F11 does not
toggle the window.

## Tests

`Swordfish.Tests/ClientFakes.cs:174` provides the test stub for the video mode
list. `Swordfish.Tests/ClientFakes.cs:185` provides the stub for the batched
display update. `Swordfish.Tests/DisplaySettingsTests.cs` covers video mode
matching, refresh-rate selection, unsupported-mode fallback, and persisted
fullscreen resolution loading.
