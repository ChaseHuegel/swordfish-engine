using System.Numerics;
using Microsoft.Extensions.Logging;
using Silk.NET.Core;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Library.Extensions;
using Swordfish.Library.IO;
using Swordfish.Library.Types;
using Swordfish.Settings;
using Swordfish.UI;
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
    private IUIContext UIContext { get; }
    private IShortcutService ShortcutService { get; }
    private ILogger Logger { get; }
    private WindowSettings WindowSettings { get; }
    private RenderSettings RenderSettings { get; }

    private readonly GL _gl;
    private readonly SynchronizationContext _mainThread;

    public SilkWindowContext(
        GL gl,
        SynchronizationContext mainThread,
        IUIContext uiContext,
        IShortcutService shortcutService,
        IWindow window,
        ILogger logger,
        WindowSettings windowSettings,
        RenderSettings renderSettings
    ) {
        _gl = gl;
        _mainThread = mainThread;
        Window = window;
        UIContext = uiContext;
        ShortcutService = shortcutService;
        Logger = logger;
        WindowSettings = windowSettings;
        RenderSettings = renderSettings;

        window.FocusChanged += OnFocusChanged;
        window.Closing += OnClose;
        window.Update += OnUpdate;
        window.Render += OnRender;
        window.Resize += OnResize;

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
        ApplyBorderSettings(windowSettings.Borderless, windowSettings.AllowResize);

        windowSettings.Mode.Changed += OnModeChanged;
        ApplyMode(windowSettings.Mode);

        ShortcutService.RegisterShortcut(new Shortcut(
                "Toggle Fullscreen",
                "UI",
                ShortcutModifiers.None,
                Key.F11,
                Shortcut.DefaultEnabled,
                () => WindowSettings.Mode.Set(WindowSettings.Mode != WindowMode.Maximized ? WindowMode.Maximized : WindowMode.Windowed)
            )
        );

        ShortcutService.RegisterShortcut(new Shortcut(
                "Alt Toggle Fullscreen",
                "UI",
                ShortcutModifiers.Alt,
                Key.Enter,
                Shortcut.DefaultEnabled,
                () => WindowSettings.Mode.Set(WindowSettings.Mode != WindowMode.Fullscreen ? WindowMode.Fullscreen : WindowMode.Maximized)
            )
        );

        Logger.LogInformation("Window initialized using OpenGL {gl}, GLSL {glsl}, and Renderer {renderer}",
            _gl.GetStringS(StringName.Version),
            _gl.GetStringS(StringName.ShadingLanguageVersion),
            _gl.GetStringS(StringName.Renderer)
        );

        Logger.LogDebug("OpenGL MaxVertexAttribs: {maxVertexAttribs}", _gl.GetInt(GetPName.MaxVertexAttribs));
        Logger.LogDebug("OpenGL extensions: {extensions}", string.Join(", ", _gl.GetExtensions()));

        UIContext.Initialize();
        Loaded?.Invoke();
    }

    public Vector2 GetSize()
    {
        return (Vector2)Window.Size;
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

        Render?.Invoke(delta);
    }

    private void OnResize(Vector2D<int> size)
    {
        _gl.Viewport(size);
        Resized?.Invoke(new Vector2(size.X, size.Y));
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
        ApplyPosition(x: e.NewValue, WindowSettings.Y);
    }

    private void OnYChanged(object? sender, DataChangedEventArgs<int?> e)
    {
        ApplyPosition(WindowSettings.X, y: e.NewValue);
    }
    
    private void OnWidthChanged(object? sender, DataChangedEventArgs<int?> e)
    {
        ApplySize(width: e.NewValue, WindowSettings.Height);
    }

    private void OnHeightChanged(object? sender, DataChangedEventArgs<int?> e)
    {
        ApplySize(WindowSettings.Width, height: e.NewValue);
    }
    
    private void OnBorderlessChanged(object? sender, DataChangedEventArgs<bool> e)
    {
        ApplyBorderSettings(borderless: e.NewValue, WindowSettings.AllowResize);
    }
    
    private void OnAllowResizeChanged(object? sender, DataChangedEventArgs<bool> e)
    {
        ApplyBorderSettings(WindowSettings.Borderless, allowResize: e.NewValue);
    }
    
    private void OnModeChanged(object? sender, DataChangedEventArgs<WindowMode> e)
    {
        ApplyMode(e.NewValue);
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
    
    private void ApplyBorderSettings(bool borderless, bool allowResize) 
    {
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
        
        WindowSettings.Save();
    }

    private void ApplyMode(WindowMode mode)
    {
        Window.WindowState = mode switch
        {
            WindowMode.Windowed => WindowState.Normal,
            WindowMode.Maximized => WindowState.Maximized,
            WindowMode.Fullscreen => WindowState.Fullscreen,
            _ => WindowState.Normal,
        };

        WindowSettings.Save();
    }
}
