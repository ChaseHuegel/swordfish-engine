# Window display

One subject: the `IWindowContext` display contract for video modes and batched
display changes.

## Contract

`Swordfish/Graphics/IWindowContext.cs:27` exposes `GetMonitors`. It returns the
unique display names of all connected monitors in enumeration order.

`Swordfish/Graphics/IWindowContext.cs:30` exposes `GetVideoModes`. It returns
the distinct fullscreen resolutions that the requested monitor (or current
monitor when omitted) reports.

`Swordfish/Graphics/IWindowContext.cs:38` exposes `ApplyDisplaySettings`. It
applies a `WindowMode`, a borderless flag, an optional resolution, and an
optional preferred monitor name as one update and returns a `Result`. A null
size or monitor name keeps the saved value for the target mode.

`Swordfish/Settings/WindowSettings.cs:16` stores `FullscreenWidth` and
`FullscreenHeight` separately from the windowed `Width` and `Height` bindings so
exclusive fullscreen modes persist across restarts without overwriting the
windowed size. `WindowSettings.Monitor` at
`Swordfish/Settings/WindowSettings.cs:19` stores the preferred monitor name, or
an empty string for automatic selection.

## Behavior

`SilkWindowContext.GetMonitors` is at
`Swordfish/Graphics/SilkWindowContext.cs:173`. It enumerates connected GLFW
monitors and disambiguates duplicate or blank names through
`FormatMonitorNames` at `Swordfish/Graphics/SilkWindowContext.cs:201`.

`SilkWindowContext.GetVideoModes` is at
`Swordfish/Graphics/SilkWindowContext.cs:229`. It queries the target monitor
selected by `ResolveTargetNativeMonitor` at
`Swordfish/Graphics/SilkWindowContext.cs:991` (falling back to
`FindCurrentNativeMonitor` at `Swordfish/Graphics/SilkWindowContext.cs:1044`),
drops null and duplicate resolutions, sorts by width then height, and falls back
to the current window size. `MonitorResolution` uses the same monitor lookup.

`SilkWindowContext.ResolveFullscreenVideoMode` is at
`Swordfish/Graphics/SilkWindowContext.cs:291`. It matches a requested exclusive
fullscreen resolution against the monitor's supported video modes, preferring
the monitor's current refresh rate and otherwise the highest available refresh
rate. If the requested resolution is unsupported, it selects a supported
fallback mode and returns a failure `Result`.

`SilkWindowContext.ApplyDisplaySettings` is at
`Swordfish/Graphics/SilkWindowContext.cs:346`. For exclusive fullscreen
(`WindowMode.Fullscreen` without borderless), it delegates to
`ApplyExclusiveFullscreenSettings` at
`Swordfish/Graphics/SilkWindowContext.cs:417`, resolves the video mode against
the target monitor, persists `Monitor`, `FullscreenWidth`, and
`FullscreenHeight`, and applies the monitor video mode directly through GLFW in
`ApplyExclusiveFullscreen` at `Swordfish/Graphics/SilkWindowContext.cs:946`
without calling `Window.WindowState = WindowState.Fullscreen` (which forces the
primary monitor). When leaving exclusive fullscreen,
`ExitExclusiveFullscreenIfNeeded` at
`Swordfish/Graphics/SilkWindowContext.cs:1019` restores the window to its saved
position on the same monitor before applying the target style. On startup,
`SilkWindowContext` restores saved exclusive fullscreen settings through the
same path at `Swordfish/Graphics/SilkWindowContext.cs:106`.

For windowed, maximized, and borderless fullscreen modes,
`ApplyDisplaySettings` sets the monitor, borderless, and mode bindings while
`_suppressWindowEvents` is true and applies the window style once. When the
target mode is `Windowed`, it then clamps the requested size to the restored
monitor bounds, persists `Width` and `Height`, applies the size, and centers the
window. The position is saved with the same suppression.

The size must be applied after the window style. Silk.NET restores the saved
non-fullscreen size and desktop video mode when the window leaves fullscreen.
Clamping or applying the windowed size first therefore clamps against the old
fullscreen video mode or loses the requested size.

The suppression checks are in the X, Y, width, height, fullscreen width,
fullscreen height, monitor, borderless, allow-resize, and mode change handlers
at `Swordfish/Graphics/SilkWindowContext.cs:771`, `:781`, `:791`, `:801`,
`:811`, `:821`, `:831`, `:841`, `:859`, and `:877`. This prevents a display
change from resizing or moving the window several times.

`SwordfishEngine` creates the window at `Swordfish/SwordfishEngine.cs:63` with
an OpenGL 4.3 Core context, `VSync = true`, and `Samples = 0`. The Forward+
pipeline owns offscreen multisampling, so the default framebuffer stays
single-sampled.

`SilkWindowContext.OverrideRefreshCallback` at
`Swordfish/Graphics/SilkWindowContext.cs:151` replaces GLFW's window refresh
callback with a static no-op delegate on all platforms. This stops Silk.NET from
rendering re-entrantly inside resize and maximize event pumps. `OnRender` at
`Swordfish/Graphics/SilkWindowContext.cs:717` ignores pending resize events
when either dimension is zero or negative.

The method logs the requested and actual window state at
`Swordfish/Graphics/SilkWindowContext.cs:400` and `:492`. Use those lines to
diagnose a platform that leaves a mixed state.

## Shortcuts

`ToggleMaximized` at `Swordfish/Graphics/SilkWindowContext.cs:674` switches
between `Maximized` and `Windowed`. It is bound to F11.

`ToggleFullscreen` at `Swordfish/Graphics/SilkWindowContext.cs:684` switches
between `Fullscreen` and `Maximized`. It is bound to Alt+Enter. Both call
`ApplyDisplaySettings` without a size or monitor override, so the windowed size,
fullscreen size, and preferred monitor are preserved.

The key match rule is in [input-shortcuts](input-shortcuts.md). A held
modifier blocks a shortcut that declares none. For example, Shift+F11 does not
toggle the window.

## Tests

`Swordfish.Tests/ClientFakes.cs:176` provides the test stub for the monitor
list. `Swordfish.Tests/ClientFakes.cs:182` provides the stub for the video mode
list. `Swordfish.Tests/ClientFakes.cs:193` provides the stub for the batched
display update. `Swordfish.Tests/DisplaySettingsTests.cs` covers monitor name
formatting, video mode matching, refresh-rate selection, unsupported-mode
fallback, and persisted fullscreen resolution loading.
