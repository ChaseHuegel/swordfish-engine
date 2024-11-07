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
using Swordfish.UI;
using Swordfish.Util;
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
    private IInputService InputService { get; }
    private IShortcutService ShortcutService { get; }
    private ILogger Logger { get; }

    private readonly GL GL;
    private readonly SynchronizationContext MainThread;

    public SilkWindowContext(GL gl, SynchronizationContext mainThread, IUIContext uiContext, IInputService inputService, IShortcutService shortcutService, IWindow window, ILogger logger)
    {
        GL = gl;
        MainThread = mainThread;
        Window = window;
        UIContext = uiContext;
        InputService = inputService;
        ShortcutService = shortcutService;
        Logger = logger;

        Window.FocusChanged += OnFocusChanged;
        Window.Closing += OnClose;
        Window.Update += OnUpdate;
        Window.Render += OnRender;
        Window.Resize += OnResize;

        Window.Center();

        //  TODO refactor "manifest" files. Its now confusing with Module manifests.
        RawImage icon = Imaging.LoadAsPng(new PathInfo("manifest://swordfish.ico").Open());
        Window.SetWindowIcon(ref icon);

        ShortcutService.RegisterShortcut(new Shortcut(
                "Toggle Fullscreen",
                "UI",
                ShortcutModifiers.NONE,
                Key.F11,
                Shortcut.DefaultEnabled,
                () => Window.WindowState = Window.WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal
            )
        );

        ShortcutService.RegisterShortcut(new Shortcut(
                "Alt Toggle Fullscreen",
                "UI",
                ShortcutModifiers.ALT,
                Key.ENTER,
                Shortcut.DefaultEnabled,
                () => Window.WindowState = Window.WindowState == WindowState.Normal ? WindowState.Fullscreen : WindowState.Normal
            )
        );

        Logger.LogInformation("Window initialized using OpenGL {gl}, GLSL {glsl}, and Renderer {renderer}",
            GL.GetStringS(StringName.Version),
            GL.GetStringS(StringName.ShadingLanguageVersion),
            GL.GetStringS(StringName.Renderer)
        );

        Logger.LogDebug("OpenGL MaxVertexAttribs: {maxVertexAttribs}", GL.GetInt(GetPName.MaxVertexAttribs));
        Logger.LogDebug("OpenGL extensions: {extensions}", string.Join(", ", GL.GetExtensions()));

        UIContext.Initialize();
        Loaded?.Invoke();
    }

    public Vector2 GetSize()
    {
        return (Vector2)Window.Size;
    }

    public void Close()
    {
        MainThread.WaitFor(Window.Close);
    }

    public void SetWindowed()
    {
        MainThread.WaitFor(() => Window.WindowState = WindowState.Normal);
    }

    public void Minimize()
    {
        MainThread.WaitFor(() => Window.WindowState = WindowState.Minimized);
    }

    public void Maximize()
    {
        MainThread.WaitFor(() => Window.WindowState = WindowState.Maximized);
    }

    public void Fullscreen()
    {
        MainThread.WaitFor(() => Window.WindowState = WindowState.Fullscreen);
    }

    private void OnClose()
    {
        GL.Dispose();
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
        GL.Viewport(size);
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
}
