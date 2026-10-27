using System.Numerics;
using Microsoft.Extensions.Logging;
using Silk.NET.Core;
using Silk.NET.GLFW;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Library.Extensions;
using Swordfish.Library.IO;
using Swordfish.Library.Types;
using Swordfish.Settings;
using Key = Swordfish.Library.IO.Key;

namespace Swordfish.Graphics;

public class SilkWindowContext : IWindowContext
{
    public DataBinding<double> UpdateDelta { get; } = new();
    public DataBinding<double> RenderDelta { get; } = new();

    public Vector2 Resolution => new(Window.Size.X, Window.Size.Y);

    public Vector2 MonitorResolution => (Vector2?)Window.Monitor?.VideoMode.Resolution ?? Vector2.Zero;

    public Action? Loaded { get; set; }
    public Action? Closed { get; set; }
    public Action<double>? Render { get; set; }
    public Action<double>? Update { get; set; }
    public Action? Focused { get; set; }
    public Action? Unfocused { get; set; }
    public Action<Vector2>? Resized { get; set; }

    private IWindow Window { get; }
    private IShortcutService ShortcutService { get; }
    private ILogger Logger { get; }
    private WindowSettings WindowSettings { get; }
    private RenderSettings RenderSettings { get; }

    private readonly GL _gl;
    private readonly SynchronizationContext _mainThread;
    private Vector2D<int>? _pendingResizeSize;
    private bool _suppressWindowEvents;

    public SilkWindowContext(
        GL gl,
        SynchronizationContext mainThread,
        IShortcutService shortcutService,
        IWindow window,
        ILogger logger,
        WindowSettings windowSettings,
        RenderSettings renderSettings
    ) {
        _gl = gl;
        _mainThread = mainThread;
        Window = window;
        ShortcutService = shortcutService;
        Logger = logger;
        WindowSettings = windowSettings;
        RenderSettings = renderSettings;

        window.FocusChanged += OnFocusChanged;
        window.Closing += OnClose;
        window.Update += OnUpdate;
        window.Render += OnRender;
        window.Resize += OnResize;

        OverrideRefreshCallback();

        renderSettings.VSync.Changed += OnVSyncChanged;
        ApplyVSync(renderSettings.VSync);

        renderSettings.Framerate.Changed += OnFramerateChanged;
        ApplyFramerate(renderSettings.Framerate);

        windowSettings.AlwaysOnTop.Changed += OnAlwaysOnTopChanged;
        ApplyAlwaysOnTop(windowSettings.AlwaysOnTop);

        windowSettings.Title.Changed += OnTitleChanged;
        ApplyTitle(windowSettings.Title);

        windowSettings.X.Changed += OnXChanged;
        windowSettings.Y.Changed += OnYChanged;
        ApplyPosition(windowSettings.X, windowSettings.Y);

        windowSettings.Width.Changed += OnWidthChanged;
        windowSettings.Height.Changed += OnHeightChanged;
        ApplySize(windowSettings.Width, windowSettings.Height);

        windowSettings.Borderless.Changed += OnBorderlessChanged;
        windowSettings.AllowResize.Changed += OnAllowResizeChanged;
        windowSettings.Mode.Changed += OnModeChanged;
        ApplyWindowStyle(windowSettings.Mode, windowSettings.Borderless, windowSettings.AllowResize);

        ShortcutService.RegisterShortcut(new Shortcut(
                "Toggle Fullscreen",
                "UI",
                ShortcutModifiers.None,
                Key.F11,
                Shortcut.DefaultEnabled,
                ToggleMaximized
            )
        );

        ShortcutService.RegisterShortcut(new Shortcut(
                "Alt Toggle Fullscreen",
                "UI",
                ShortcutModifiers.Alt,
                Key.Enter,
                Shortcut.DefaultEnabled,
                ToggleFullscreen
            )
        );

        Logger.LogInformation("Window initialized using OpenGL {gl}, GLSL {glsl}, and Renderer {renderer}",
            _gl.GetStringS(StringName.Version),
            _gl.GetStringS(StringName.ShadingLanguageVersion),
            _gl.GetStringS(StringName.Renderer)
        );

        Logger.LogDebug("OpenGL MaxVertexAttribs: {maxVertexAttribs}", _gl.GetInt(GetPName.MaxVertexAttribs));
        Logger.LogDebug("OpenGL extensions: {extensions}", string.Join(", ", _gl.GetExtensions()));

        Loaded?.Invoke();
    }

    private void OverrideRefreshCallback()
    {
        //  GLFW's window refresh callback makes Silk.NET render a full frame re-entrantly from inside
        //  the event pump, which breaks interactive resizing on Linux. The engine redraws every frame
        //  regardless, so the refresh callback can be a no-op.
        //  See https://github.com/dotnet/Silk.NET/issues/2581 and https://github.com/dotnet/Silk.NET/pull/2582
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        unsafe
        {
            GlfwCallbacks.WindowRefreshCallback noOpRefreshCallback = _ => { };
            Glfw.GetApi().SetWindowRefreshCallback((WindowHandle*)Window.Handle, noOpRefreshCallback);
        }
    }

    public Vector2 GetSize()
    {
        return (Vector2)Window.Size;
    }

    /// <inheritdoc/>
    public IReadOnlyList<Vector2> GetVideoModes()
    {
        IMonitor? monitor = Window.Monitor;
        if (monitor == null)
        {
            return [Resolution];
        }

        var videoModes = monitor.GetAllVideoModes();
        var resolutions = new List<Vector2>();
        var seen = new HashSet<(int Width, int Height)>();

        foreach (var videoMode in videoModes)
        {
            Vector2D<int>? resolution = videoMode.Resolution;
            if (resolution == null)
            {
                continue;
            }

            int width = resolution.Value.X;
            int height = resolution.Value.Y;
            if (width <= 0 || height <= 0 || !seen.Add((width, height)))
            {
                continue;
            }

            resolutions.Add(new Vector2(width, height));
        }

        if (resolutions.Count == 0)
        {
            resolutions.Add(Resolution);
        }

        resolutions.Sort(static (left, right) =>
        {
            int widthComparison = left.X.CompareTo(right.X);
            return widthComparison != 0 ? widthComparison : left.Y.CompareTo(right.Y);
        });

        return resolutions;
    }

    /// <inheritdoc/>
    public void ApplyDisplaySettings(WindowMode mode, bool borderless, int? width = null, int? height = null)
    {
        int targetWidth = width ?? WindowSettings.Width.Get() ?? Window.Size.X;
        int targetHeight = height ?? WindowSettings.Height.Get() ?? Window.Size.Y;

        if (Window.Monitor != null)
        {
            Vector2D<int> monitorSize = Window.Monitor.VideoMode.Resolution ?? Window.Monitor.Bounds.Size;
            targetWidth = Math.Clamp(targetWidth, 320, monitorSize.X);
            targetHeight = Math.Clamp(targetHeight, 240, monitorSize.Y);
        }

        _suppressWindowEvents = true;
        try
        {
            if (mode == WindowMode.Windowed)
            {
                WindowSettings.Width.Set(targetWidth);
                WindowSettings.Height.Set(targetHeight);
            }

            if (mode != WindowMode.Fullscreen || !borderless)
            {
                WindowSettings.Borderless.Set(borderless);
            }

            WindowSettings.Mode.Set(mode);

            ApplyWindowStyle(mode, borderless, WindowSettings.AllowResize.Get());

            if (mode == WindowMode.Windowed)
            {
                ApplySize(targetWidth, targetHeight);
                Window.Center();

                WindowSettings.X.Set(Window.Position.X);
                WindowSettings.Y.Set(Window.Position.Y);
            }

            WindowSettings.Save();
        }
        finally
        {
            _suppressWindowEvents = false;
        }

        Logger.LogDebug(
            "Applied display settings. Requested mode={mode}, borderless={borderless}, size={width}x{height}. Actual state={state}, border={border}, size={actualWidth}x{actualHeight}, position={position}.",
            mode,
            borderless,
            targetWidth,
            targetHeight,
            Window.WindowState,
            Window.WindowBorder,
            Window.Size.X,
            Window.Size.Y,
            Window.Position
        );
    }

    /// <summary>Toggles the window between maximized and windowed.</summary>
    private void ToggleMaximized()
    {
        WindowMode mode = WindowSettings.Mode.Get() == WindowMode.Maximized
            ? WindowMode.Windowed
            : WindowMode.Maximized;

        ApplyDisplaySettings(mode, borderless: false);
    }

    /// <summary>Toggles the window between fullscreen and maximized.</summary>
    private void ToggleFullscreen()
    {
        WindowMode mode = WindowSettings.Mode.Get() == WindowMode.Fullscreen
            ? WindowMode.Maximized
            : WindowMode.Fullscreen;

        ApplyDisplaySettings(mode, borderless: false);
    }

    public void Close()
    {
        _mainThread.WaitFor(Window.Close);
    }

    public void SetIcon(Texture icon)
    {
        var rawImage = new RawImage(icon.Width, icon.Height, icon.Pixels);
        Window.SetWindowIcon(ref rawImage);
    }

    private void OnClose()
    {
        _gl.Dispose();
        Closed?.Invoke();
    }

    private void OnUpdate(double delta)
    {
        UpdateDelta.Set(delta);
        Update?.Invoke(delta);
    }

    private void OnRender(double delta)
    {
        RenderDelta.Set(delta);

        if (_pendingResizeSize != null)
        {
            Vector2D<int> size = _pendingResizeSize.Value;
            _pendingResizeSize = null;
            _gl.Viewport(size);
            Resized?.Invoke(new Vector2(size.X, size.Y));
        }

        Render?.Invoke(delta);
    }

    private void OnResize(Vector2D<int> size)
    {
        _pendingResizeSize = size;
    }

    private void OnFocusChanged(bool focused)
    {
        if (focused)
        {
            Focused?.Invoke();
        }
        else
        {
            Unfocused?.Invoke();
        }
    }

    private void OnVSyncChanged(object? sender, DataChangedEventArgs<bool> e)
    {
        ApplyVSync(e.NewValue);
    }

    private void OnFramerateChanged(object? sender, DataChangedEventArgs<int> e)
    {
        ApplyFramerate(e.NewValue);
    }

    private void OnAlwaysOnTopChanged(object? sender, DataChangedEventArgs<bool> e)
    {
        ApplyAlwaysOnTop(e.NewValue);
    }
    private void OnTitleChanged(object? sender, DataChangedEventArgs<string?> e)
    {
        ApplyTitle(e.NewValue);
    }

    private void OnXChanged(object? sender, DataChangedEventArgs<int?> e)
    {
        if (_suppressWindowEvents)
        {
            return;
        }

        ApplyPosition(x: e.NewValue, WindowSettings.Y);
    }

    private void OnYChanged(object? sender, DataChangedEventArgs<int?> e)
    {
        if (_suppressWindowEvents)
        {
            return;
        }

        ApplyPosition(WindowSettings.X, y: e.NewValue);
    }

    private void OnWidthChanged(object? sender, DataChangedEventArgs<int?> e)
    {
        if (_suppressWindowEvents)
        {
            return;
        }

        ApplySize(width: e.NewValue, WindowSettings.Height);
    }

    private void OnHeightChanged(object? sender, DataChangedEventArgs<int?> e)
    {
        if (_suppressWindowEvents)
        {
            return;
        }

        ApplySize(WindowSettings.Width, height: e.NewValue);
    }

    private void OnBorderlessChanged(object? sender, DataChangedEventArgs<bool> e)
    {
        if (_suppressWindowEvents)
        {
            return;
        }

        WindowMode mode = WindowSettings.Mode.Get();
        bool allowResize = WindowSettings.AllowResize.Get();
        ApplyWindowStyle(mode, borderless: e.NewValue, allowResize);
    }

    private void OnAllowResizeChanged(object? sender, DataChangedEventArgs<bool> e)
    {
        WindowMode mode = WindowSettings.Mode.Get();
        bool borderless = WindowSettings.Borderless.Get();
        ApplyWindowStyle(mode, borderless, allowResize: e.NewValue);
    }

    private void OnModeChanged(object? sender, DataChangedEventArgs<WindowMode> e)
    {
        if (_suppressWindowEvents)
        {
            return;
        }

        bool borderless = WindowSettings.Borderless.Get();
        bool allowResize = WindowSettings.AllowResize.Get();
        ApplyWindowStyle(mode: e.NewValue, borderless, allowResize);
    }

    private void ApplyVSync(bool vsync)
    {
        Window.VSync = vsync;
        RenderSettings.Save();
    }

    private void ApplyFramerate(int framerate)
    {
        Window.FramesPerSecond = framerate;
        RenderSettings.Save();
    }

    private void ApplyAlwaysOnTop(bool alwaysOnTop)
    {
        Window.TopMost = alwaysOnTop;
    }

    private void ApplyTitle(string? title)
    {
        if (title == null)
        {
            return;
        }

        Window.Title = title;
    }

    private void ApplyPosition(int? x, int? y)
    {
        if (x != null && y != null)
        {
            Window.Position = new Vector2D<int>(x.Value, y.Value);
            WindowSettings.Save();
        }
        else
        {
            Window.Center();
        }
    }

    private void ApplySize(int? width, int? height)
    {
        if (width == null || height == null)
        {
            return;
        }

        Window.Size = new Vector2D<int>(width.Value, height.Value);
        WindowSettings.Save();
    }

    private void ApplyWindowStyle(WindowMode mode, bool borderless, bool allowResize)
    {
        //  Borderless fullscreen must be handled specially
        if (mode == WindowMode.Fullscreen && borderless && Window.Monitor != null)
        {
            Window.WindowState = WindowState.Normal;
            Window.WindowState = WindowState.Maximized;
            Window.WindowBorder = WindowBorder.Hidden;
            Window.Position = Window.Monitor.Bounds.Origin;
            Window.Size = Window.Monitor.VideoMode.Resolution ?? Window.Monitor.Bounds.Size;
        }
        else
        {
            Window.WindowState = WindowState.Normal;

            if (borderless)
            {
                Window.WindowBorder = WindowBorder.Hidden;
            }
            else if (allowResize)
            {
                Window.WindowBorder = WindowBorder.Resizable;
            }
            else
            {
                Window.WindowBorder = WindowBorder.Fixed;
            }

            Window.WindowState = mode switch
            {
                WindowMode.Windowed => WindowState.Normal,
                WindowMode.Maximized => WindowState.Maximized,
                WindowMode.Fullscreen => WindowState.Fullscreen,
                _ => WindowState.Normal,
            };
        }

        WindowSettings.Save();
    }
}
