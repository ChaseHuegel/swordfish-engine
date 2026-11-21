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
using Swordfish.Library.Util;
using Swordfish.Settings;
using Key = Swordfish.Library.IO.Key;
using VideoMode = Silk.NET.Windowing.VideoMode;

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

        windowSettings.FullscreenWidth.Changed += OnFullscreenWidthChanged;
        windowSettings.FullscreenHeight.Changed += OnFullscreenHeightChanged;
        
        windowSettings.Borderless.Changed += OnBorderlessChanged;
        windowSettings.AllowResize.Changed += OnAllowResizeChanged;
        windowSettings.Mode.Changed += OnModeChanged;
        if (windowSettings.Mode.Get() == WindowMode.Fullscreen && !windowSettings.Borderless.Get())
        {
            ApplyDisplaySettings(
                WindowMode.Fullscreen,
                borderless: false,
                windowSettings.FullscreenWidth.Get(),
                windowSettings.FullscreenHeight.Get()
            );
        }
        else
        {
            ApplyWindowStyle(windowSettings.Mode, windowSettings.Borderless, windowSettings.AllowResize);
        }

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

    /// <summary>Result of resolving a requested exclusive fullscreen video mode.</summary>
    internal readonly record struct FullscreenModeResolution(VideoMode Mode, Result Status);

    /// <summary>Resolves a requested exclusive fullscreen resolution against supported monitor video modes.</summary>
    /// <param name="requestedWidth">The requested width, or null to use the monitor's current mode.</param>
    /// <param name="requestedHeight">The requested height, or null to use the monitor's current mode.</param>
    /// <param name="supportedModes">The video modes reported by the monitor.</param>
    /// <param name="currentMode">The monitor's current video mode, used for refresh-rate preference and fallback.</param>
    internal static FullscreenModeResolution ResolveFullscreenVideoMode(
        int? requestedWidth,
        int? requestedHeight,
        IReadOnlyList<VideoMode> supportedModes,
        VideoMode currentMode
    )
    {
        if (requestedWidth == null || requestedHeight == null)
        {
            VideoMode? defaultMode = FindFallbackVideoMode(supportedModes, currentMode);
            if (defaultMode == null)
            {
                return new FullscreenModeResolution(
                    VideoMode.Default,
                    Result.FromFailure("No supported fullscreen video modes were reported by the monitor.")
                );
            }

            return new FullscreenModeResolution(defaultMode.Value, Result.FromSuccess());
        }

        if (requestedWidth.Value > 0 && requestedHeight.Value > 0)
        {
            VideoMode? matched = FindMatchingVideoMode(
                supportedModes,
                requestedWidth.Value,
                requestedHeight.Value,
                currentMode.RefreshRate
            );
            if (matched != null)
            {
                return new FullscreenModeResolution(matched.Value, Result.FromSuccess());
            }
        }

        VideoMode? fallback = FindFallbackVideoMode(supportedModes, currentMode);
        if (fallback == null)
        {
            return new FullscreenModeResolution(
                VideoMode.Default,
                Result.FromFailure(
                    $"Fullscreen resolution {requestedWidth.Value}x{requestedHeight.Value} is not supported and no fallback video mode is available."
                )
            );
        }

        Vector2D<int> fallbackResolution = fallback.Value.Resolution!.Value;
        return new FullscreenModeResolution(
            fallback.Value,
            Result.FromFailure(
                $"Fullscreen resolution {requestedWidth.Value}x{requestedHeight.Value} is not supported; fell back to {fallbackResolution.X}x{fallbackResolution.Y}."
            )
        );
    }

    /// <inheritdoc/>
    public Result ApplyDisplaySettings(WindowMode mode, bool borderless, int? width = null, int? height = null)
    {
        if (mode == WindowMode.Fullscreen && !borderless)
        {
            return ApplyExclusiveFullscreenSettings(width, height);
        }

        int targetWidth = width ?? WindowSettings.Width.Get() ?? Window.Size.X;
        int targetHeight = height ?? WindowSettings.Height.Get() ?? Window.Size.Y;

        _suppressWindowEvents = true;
        try
        {
            WindowSettings.Borderless.Set(borderless);
            WindowSettings.Mode.Set(mode);
            
            ApplyWindowStyle(mode, borderless, WindowSettings.AllowResize.Get());

            if (mode == WindowMode.Windowed)
            {
                if (Window.Monitor != null)
                {
                    Vector2D<int> monitorSize = Window.Monitor.VideoMode.Resolution ?? Window.Monitor.Bounds.Size;
                    targetWidth = Math.Clamp(targetWidth, 320, monitorSize.X);
                    targetHeight = Math.Clamp(targetHeight, 240, monitorSize.Y);
                }

                WindowSettings.Width.Set(targetWidth);
                WindowSettings.Height.Set(targetHeight);

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

        return Result.FromSuccess();
    }

    private Result ApplyExclusiveFullscreenSettings(int? width, int? height)
    {
        int? requestedWidth = width ?? WindowSettings.FullscreenWidth.Get();
        int? requestedHeight = height ?? WindowSettings.FullscreenHeight.Get();
        IMonitor? monitor = Window.Monitor;

        if (monitor == null)
        {
            _suppressWindowEvents = true;
            try
            {
                WindowSettings.Borderless.Set(false);
                WindowSettings.Mode.Set(WindowMode.Fullscreen);
                ApplyWindowStyle(WindowMode.Fullscreen, borderless: false, WindowSettings.AllowResize.Get());
                WindowSettings.Save();
            }
            finally
            {
                _suppressWindowEvents = false;
            }

            if (requestedWidth != null && requestedHeight != null)
            {
                string message = $"No monitor is available to apply fullscreen resolution {requestedWidth.Value}x{requestedHeight.Value}.";
                Logger.LogWarning("{message}", message);
                return Result.FromFailure(message);
            }

            return Result.FromSuccess();
        }

        List<VideoMode> supportedModes = CollectMonitorVideoModes(monitor);
        FullscreenModeResolution resolution = ResolveFullscreenVideoMode(
            requestedWidth,
            requestedHeight,
            supportedModes,
            monitor.VideoMode
        );

        _suppressWindowEvents = true;
        try
        {
            WindowSettings.Borderless.Set(false);
            WindowSettings.Mode.Set(WindowMode.Fullscreen);

            Vector2D<int>? appliedResolution = resolution.Mode.Resolution;
            if (appliedResolution != null && appliedResolution.Value.X > 0 && appliedResolution.Value.Y > 0)
            {
                WindowSettings.FullscreenWidth.Set(appliedResolution.Value.X);
                WindowSettings.FullscreenHeight.Set(appliedResolution.Value.Y);
            }

            ApplyExclusiveFullscreen(monitor, resolution.Mode, WindowSettings.AllowResize.Get());
            WindowSettings.Save();
        }
        finally
        {
            _suppressWindowEvents = false;
        }

        if (!resolution.Status.Success)
        {
            Logger.LogWarning("{message}", resolution.Status.Message);
        }

        Logger.LogDebug(
            "Applied exclusive fullscreen settings. Requested={requestedWidth}x{requestedHeight}, resolved={resolvedWidth}x{resolvedHeight}@{refreshRate}Hz. Actual state={state}, border={border}, size={actualWidth}x{actualHeight}, position={position}.",
            requestedWidth,
            requestedHeight,
            resolution.Mode.Resolution?.X,
            resolution.Mode.Resolution?.Y,
            resolution.Mode.RefreshRate,
            Window.WindowState,
            Window.WindowBorder,
            Window.Size.X,
            Window.Size.Y,
            Window.Position
        );

        return resolution.Status;
    }

    private static List<VideoMode> CollectMonitorVideoModes(IMonitor monitor)
    {
        var modes = new List<VideoMode>();
        foreach (VideoMode mode in monitor.GetAllVideoModes())
        {
            Vector2D<int>? resolution = mode.Resolution;
            if (resolution != null && resolution.Value.X > 0 && resolution.Value.Y > 0)
            {
                modes.Add(mode);
            }
        }

        return modes;
    }

    private static VideoMode? FindMatchingVideoMode(
        IReadOnlyList<VideoMode> supportedModes,
        int width,
        int height,
        int? preferredRefreshRate
    )
    {
        VideoMode? best = null;
        int bestRefreshRate = -1;

        for (var i = 0; i < supportedModes.Count; i++)
        {
            VideoMode candidate = supportedModes[i];
            Vector2D<int>? resolution = candidate.Resolution;
            if (resolution == null || resolution.Value.X != width || resolution.Value.Y != height)
            {
                continue;
            }

            if (preferredRefreshRate != null && candidate.RefreshRate == preferredRefreshRate)
            {
                return candidate;
            }

            int candidateRefreshRate = candidate.RefreshRate ?? 0;
            if (best == null || candidateRefreshRate > bestRefreshRate)
            {
                best = candidate;
                bestRefreshRate = candidateRefreshRate;
            }
        }

        return best;
    }

    private static VideoMode? FindFallbackVideoMode(IReadOnlyList<VideoMode> supportedModes, VideoMode currentMode)
    {
        Vector2D<int>? currentResolution = currentMode.Resolution;
        if (currentResolution != null && currentResolution.Value.X > 0 && currentResolution.Value.Y > 0)
        {
            VideoMode? matchedCurrent = FindMatchingVideoMode(
                supportedModes,
                currentResolution.Value.X,
                currentResolution.Value.Y,
                currentMode.RefreshRate
            );
            if (matchedCurrent != null)
            {
                return matchedCurrent;
            }
        }

        VideoMode? largest = null;
        for (var i = 0; i < supportedModes.Count; i++)
        {
            VideoMode candidate = supportedModes[i];
            Vector2D<int>? candidateResolution = candidate.Resolution;
            if (candidateResolution == null || candidateResolution.Value.X <= 0 || candidateResolution.Value.Y <= 0)
            {
                continue;
            }

            if (largest == null)
            {
                largest = candidate;
                continue;
            }

            Vector2D<int> largestResolution = largest.Value.Resolution!.Value;
            int widthCompare = candidateResolution.Value.X.CompareTo(largestResolution.X);
            int heightCompare = candidateResolution.Value.Y.CompareTo(largestResolution.Y);
            int refreshCompare = (candidate.RefreshRate ?? 0).CompareTo(largest.Value.RefreshRate ?? 0);

            if (widthCompare > 0
                || (widthCompare == 0 && heightCompare > 0)
                || (widthCompare == 0 && heightCompare == 0 && refreshCompare > 0))
            {
                largest = candidate;
            }
        }

        if (largest != null)
        {
            return largest;
        }

        return currentResolution != null && currentResolution.Value.X > 0 && currentResolution.Value.Y > 0
            ? currentMode
            : null;
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
        //  The container deletes GL resources after this event. Disposing the
        //  binding here removes the function pointers before those deletions.
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

    private void OnFullscreenWidthChanged(object? sender, DataChangedEventArgs<int?> e)
    {
        if (_suppressWindowEvents || WindowSettings.Mode.Get() != WindowMode.Fullscreen || WindowSettings.Borderless.Get())
        {
            return;
        }

        ApplyDisplaySettings(WindowMode.Fullscreen, borderless: false, width: e.NewValue, height: WindowSettings.FullscreenHeight.Get());
    }

    private void OnFullscreenHeightChanged(object? sender, DataChangedEventArgs<int?> e)
    {
        if (_suppressWindowEvents || WindowSettings.Mode.Get() != WindowMode.Fullscreen || WindowSettings.Borderless.Get())
        {
            return;
        }

        ApplyDisplaySettings(WindowMode.Fullscreen, borderless: false, width: WindowSettings.FullscreenWidth.Get(), height: e.NewValue);
    }
    
    private void OnBorderlessChanged(object? sender, DataChangedEventArgs<bool> e)
    {
        if (_suppressWindowEvents)
        {
            return;
        }

        WindowMode mode = WindowSettings.Mode.Get();
        if (mode == WindowMode.Fullscreen && !e.NewValue)
        {
            ApplyDisplaySettings(mode, borderless: false);
            return;
        }

        bool allowResize = WindowSettings.AllowResize.Get();
        ApplyWindowStyle(mode, borderless: e.NewValue, allowResize);
    }
    
    private void OnAllowResizeChanged(object? sender, DataChangedEventArgs<bool> e)
    {
        if (_suppressWindowEvents)
        {
            return;
        }

        WindowMode mode = WindowSettings.Mode.Get();
        bool borderless = WindowSettings.Borderless.Get();
        if (mode == WindowMode.Fullscreen && !borderless)
        {
            ApplyDisplaySettings(mode, borderless: false);
            return;
        }

        ApplyWindowStyle(mode, borderless, allowResize: e.NewValue);
    }
    
    private void OnModeChanged(object? sender, DataChangedEventArgs<WindowMode> e)
    {
        if (_suppressWindowEvents)
        {
            return;
        }

        bool borderless = WindowSettings.Borderless.Get();
        if (e.NewValue == WindowMode.Fullscreen && !borderless)
        {
            ApplyDisplaySettings(e.NewValue, borderless: false);
            return;
        }

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
    
    private void ApplyExclusiveFullscreen(IMonitor monitor, VideoMode videoMode, bool allowResize)
    {
        if (Window.WindowState != WindowState.Fullscreen)
        {
            Window.WindowState = WindowState.Normal;
            Window.WindowBorder = allowResize ? WindowBorder.Resizable : WindowBorder.Fixed;
            Window.WindowState = WindowState.Fullscreen;
        }

        Vector2D<int>? resolution = videoMode.Resolution;
        if (resolution != null && resolution.Value.X > 0 && resolution.Value.Y > 0)
        {
            ApplyGlfwVideoMode(monitor, resolution.Value.X, resolution.Value.Y, videoMode.RefreshRate ?? Glfw.DontCare);
        }

        Window.VSync = RenderSettings.VSync.Get();
    }

    private unsafe void ApplyGlfwVideoMode(IMonitor monitor, int width, int height, int refreshRate)
    {
        if (Window.Handle == 0)
        {
            return;
        }

        Glfw glfw = Glfw.GetApi();
        var windowHandle = (WindowHandle*)Window.Handle;
        Silk.NET.GLFW.Monitor* nativeMonitor = GetNativeMonitor(glfw, windowHandle, monitor);
        if (nativeMonitor == null)
        {
            return;
        }

        glfw.SetWindowMonitor(windowHandle, nativeMonitor, 0, 0, width, height, refreshRate);
    }

    private static unsafe Silk.NET.GLFW.Monitor* GetNativeMonitor(Glfw glfw, WindowHandle* windowHandle, IMonitor monitor)
    {
        Silk.NET.GLFW.Monitor** monitors = glfw.GetMonitors(out int count);
        if (monitors != null && monitor.Index >= 0 && monitor.Index < count)
        {
            return monitors[monitor.Index];
        }

        Silk.NET.GLFW.Monitor* windowMonitor = glfw.GetWindowMonitor(windowHandle);
        if (windowMonitor != null)
        {
            return windowMonitor;
        }

        return glfw.GetPrimaryMonitor();
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
