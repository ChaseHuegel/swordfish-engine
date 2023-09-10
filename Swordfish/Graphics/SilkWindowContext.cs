using System.Drawing;
using System.Numerics;
using Silk.NET.Core;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Extensions;
using Swordfish.Library.IO;
using Swordfish.Library.Types;
using Swordfish.UI;
using Swordfish.Util;
using Key = Swordfish.Library.IO.Key;
using Path = Swordfish.Library.IO.Path;

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

    private readonly GL GL;
    private readonly SynchronizationContext MainThread;

    public SilkWindowContext(GL gl, SynchronizationContext mainThread, IUIContext uiContext, IInputService inputService, IShortcutService shortcutService, IWindow window, IFileService fileService)
    {
        GL = gl;
        MainThread = mainThread;
        Window = window;
        UIContext = uiContext;
        InputService = inputService;
        ShortcutService = shortcutService;

        Window.FocusChanged += OnFocusChanged;
        Window.Closing += OnClose;
        Window.Update += OnUpdate;
        Window.Render += OnRender;
        Window.Resize += OnResize;

        Window.Center();

        RawImage icon = Imaging.LoadAsPng(fileService.Open(new Path("manifest://swordfish.ico")));
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

        Debugger.Log("Window initialized.");
        Debugger.Log($"using OpenGL {GL.GetStringS(StringName.Version)}", LogType.CONTINUED);
        Debugger.Log($"using GLSL {GL.GetStringS(StringName.ShadingLanguageVersion)}", LogType.CONTINUED);
        Debugger.Log($"using GPU {GL.GetStringS(StringName.Renderer)} ({GL.GetStringS(StringName.Vendor)})", LogType.CONTINUED);

        string[] openGLMetadata = new string[]
        {
            $"available extensions: {GL.GetExtensions().Length}",
            $"max vertex attributes: {GL.GetInt(GetPName.MaxVertexAttribs)}",
        };
        Debugger.Log(string.Join(", ", openGLMetadata), LogType.CONTINUED);

        if (GLDebug.TryCreateGLOutput())
            Debugger.Log("attached OpenGL debug output", LogType.CONTINUED);
        else
            Debugger.Log("unable to attach OpenGL debug output, logs will be minimal and generic", LogType.CONTINUED);

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

        GLDebug.TryCollectAllGLErrors("OnRender");
    }

    private void OnResize(Vector2D<int> size)
    {
        GL.Viewport(size);
        Resized?.Invoke(new Vector2(size.X, size.Y));
    }

    private void OnFocusChanged(bool obj)
    {
        if (obj)
            Focused?.Invoke();
        else
            Unfocused?.Invoke();
    }
}
