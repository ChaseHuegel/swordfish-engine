# Window display

One subject: the `IWindowContext` display contract for video modes and batched
display changes.

## Contract

`Swordfish/Graphics/IWindowContext.cs:26` exposes `GetVideoModes`. It returns
the distinct fullscreen resolutions that the current monitor reports.

`Swordfish/Graphics/IWindowContext.cs:29` exposes `ApplyDisplaySettings`. It
applies a `WindowMode`, a borderless flag, and a window size as one update.

## Behavior

`SilkWindowContext.GetVideoModes` is at
`Swordfish/Graphics/SilkWindowContext.cs:151`. It reads
`Window.Monitor.GetAllVideoModes`, drops null and duplicate resolutions, sorts
by width then height, and falls back to the current window size.

`SilkWindowContext.ApplyDisplaySettings` is at
`Swordfish/Graphics/SilkWindowContext.cs:196`. It clamps a windowed size to the
monitor, sets the width, height, borderless, and mode bindings while
`_suppressWindowEvents` is true, and applies the window style once. It applies
the requested size and centers the window only when the target mode is
`Windowed`. The position is saved with the same suppression.

The size must be applied after the window style. Silk.NET restores the saved
non-fullscreen size when the window leaves fullscreen. It ignores or does not
carry over `Window.Size` while the window is still fullscreen. Applying the
windowed size first therefore loses the requested size.

The suppression checks are in the X, Y, width, height, borderless, and mode
change handlers at `Swordfish/Graphics/SilkWindowContext.cs:329`, `:339`,
`:349`, `:359`, `:369`, and `:388`. This prevents a display change from
resizing or moving the window several times.

The method logs the requested and actual window state at
`Swordfish/Graphics/SilkWindowContext.cs:239`. Use that line to diagnose a
platform that leaves a mixed state.

## Tests

`Swordfish.Tests/ClientFakes.cs:115` provides the test stub for the video mode
list. `Swordfish.Tests/ClientFakes.cs:126` provides the stub for the batched
display update.
