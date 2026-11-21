# Window display

One subject: the `IWindowContext` display contract for video modes and batched
display changes.

## Contract

`Swordfish/Graphics/IWindowContext.cs:26` exposes `GetVideoModes`. It returns
the distinct fullscreen resolutions that the current monitor reports.

`Swordfish/Graphics/IWindowContext.cs:33` exposes `ApplyDisplaySettings`. It
applies a `WindowMode`, a borderless flag, and an optional window size as one
update. A null size keeps the saved windowed size.

## Behavior

`SilkWindowContext.GetVideoModes` is at
`Swordfish/Graphics/SilkWindowContext.cs:151`. It reads
`Window.Monitor.GetAllVideoModes`, drops null and duplicate resolutions, sorts
by width then height, and falls back to the current window size.

`SilkWindowContext.ApplyDisplaySettings` is at
`Swordfish/Graphics/SilkWindowContext.cs:196`. It resolves a null size from the
window settings, clamps the result to the monitor, sets the width, height,
borderless, and mode bindings while `_suppressWindowEvents` is true, and
applies the window style once. It applies the requested size and centers the
window only when the target mode is `Windowed`. The position is saved with the
same suppression.

The size must be applied after the window style. Silk.NET restores the saved
non-fullscreen size when the window leaves fullscreen. It ignores or does not
carry over `Window.Size` while the window is still fullscreen. Applying the
windowed size first therefore loses the requested size.

The suppression checks are in the X, Y, width, height, borderless, and mode
change handlers at `Swordfish/Graphics/SilkWindowContext.cs:354`, `:364`,
`:374`, `:384`, `:394`, and `:413`. This prevents a display change from
resizing or moving the window several times.

`SwordfishEngine` creates the window at `Swordfish/SwordfishEngine.cs:63` with
an OpenGL 4.3 Core context, `VSync = true`, and `Samples = 0`. The Forward+
pipeline owns offscreen multisampling, so the default framebuffer stays
single-sampled.

`SilkWindowContext.OverrideRefreshCallback` at
`Swordfish/Graphics/SilkWindowContext.cs:129` replaces GLFW's window refresh
callback with a static no-op delegate on all platforms. This stops Silk.NET from
rendering re-entrantly inside resize and maximize event pumps. `OnRender` at
`Swordfish/Graphics/SilkWindowContext.cs:306` ignores pending resize events
when either dimension is zero or negative.

The method logs the requested and actual window state at
`Swordfish/Graphics/SilkWindowContext.cs:242`. Use that line to diagnose a
platform that leaves a mixed state.

## Shortcuts

`ToggleMaximized` at `Swordfish/Graphics/SilkWindowContext.cs:257` switches
between `Maximized` and `Windowed`. It is bound to F11.

`ToggleFullscreen` at `Swordfish/Graphics/SilkWindowContext.cs:267` switches
between `Fullscreen` and `Maximized`. It is bound to Alt+Enter. Both call
`ApplyDisplaySettings` without a size, so the windowed size is preserved.

The key match rule is in [input-shortcuts](input-shortcuts.md). A held
modifier blocks a shortcut that declares none. For example, Shift+F11 does not
toggle the window.

## Tests

`Swordfish.Tests/ClientFakes.cs:115` provides the test stub for the video mode
list. `Swordfish.Tests/ClientFakes.cs:126` provides the stub for the batched
display update.
