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
`Swordfish/Graphics/SilkWindowContext.cs:196`. It sets the width, height,
borderless, and mode bindings while `_suppressWindowEvents` is true. It then
applies the size and the window style once each.

The suppression checks are in the width, height, borderless, and mode change
handlers at `Swordfish/Graphics/SilkWindowContext.cs:301`, `:311`, `:321`, and
`:340`. This prevents a display change from resizing the window several times.

## Tests

`Swordfish.Tests/ClientFakes.cs:115` provides the test stub for the video mode
list. `Swordfish.Tests/ClientFakes.cs:126` provides the stub for the batched
display update.
