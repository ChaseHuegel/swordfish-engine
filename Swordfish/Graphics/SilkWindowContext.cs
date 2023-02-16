using System.Drawing;
using System.Numerics;
using MicroResolver;
using Silk.NET.Core;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Swordfish.Graphics.SilkNET;
using Swordfish.Input;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.IO;
using Swordfish.UI;
using Swordfish.Util;

using Key = Swordfish.Library.IO.Key;

namespace Swordfish.Graphics;

public class SilkWindowContext : IWindowContext<GL>
{
    public GL API => GL!;

    public Vector2 Resolution => new(Window.Size.X, Window.Size.Y);

    public Vector2 MonitorResolution => (Vector2?)Window.Monitor?.VideoMode.Resolution ?? Vector2.Zero;

    public Action? Loaded { get; set; }
    public Action? Closed { get; set; }
    public Action<double>? Render { get; set; }
    public Action<double>? Update { get; set; }
    public Action? Focused { get; set; }
    public Action? Unfocused { get; set; }

    private IWindow Window { get; }
    private IRenderContext Renderer { get; }
    private IUIContext UIContext { get; }
    private SilkInputService InputService { get; }
    private IShortcutService ShortcutService { get; }

    private GL? GL;

    public SilkWindowContext(IRenderContext renderer, IUIContext uiContext, IInputService inputService, IShortcutService shortcutService)
    {
        Renderer = renderer;
        UIContext = uiContext;
        InputService = (SilkInputService)inputService;
        ShortcutService = shortcutService;

        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(800, 600);
        options.Title = "Swordfish";
        options.ShouldSwapAutomatically = true;

        Window = Silk.NET.Windowing.Window.Create(options);

        Window.FocusChanged += OnFocusChanged;
        Window.Load += OnLoad;
        Window.Closing += OnClose;
        Window.Update += OnUpdate;
        Window.Render += OnRender;
        Window.Resize += OnResize;
    }

    public void Initialize()
    {
        Window.Run();
    }

    public Vector2 GetSize()
    {
        return (Vector2)Window.Size;
    }

    public void Close()
    {
        Window.Close();
    }

    public void SetWindowed()
    {
        Window.WindowState = WindowState.Normal;
    }

    public void Minimize()
    {
        Window.WindowState = WindowState.Minimized;
    }

    public void Maximize()
    {
        Window.WindowState = WindowState.Maximized;
    }

    public void Fullscreen()
    {
        Window.WindowState = WindowState.Fullscreen;
    }

    private unsafe void OnLoad()
    {
        Window.Center();

        var pathService = SwordfishEngine.Kernel.Get<IPathService>();
        RawImage icon = Imaging.LoadAsPng(pathService.Root.At("swordfish.ico"));
        Window.SetWindowIcon(ref icon);

        IInputContext input = Window.CreateInput();

        GL = Window.CreateOpenGL();
        SwordfishEngine.Kernel.AddSingleton(GL);
        // SwordfishEngine.Kernel.AddSingleton<IWindowContext<GL>>(this);

        ShortcutService.RegisterShortcut(new Shortcut(
                "Toggle Fullscreen",
                "UI",
                ShortcutModifiers.NONE,
                Key.F11,
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

        Renderer.Initialize();
        UIContext.Initialize(Window, input);
        InputService.Initialize(input);
        Loaded?.Invoke();
    }

    private void OnClose()
    {
        GL.Dispose();
        Closed?.Invoke();
    }

    private void OnUpdate(double delta)
    {
        Update?.Invoke(delta);
    }

    private void OnRender(double delta)
    {
        GL.ClearColor(Color.CornflowerBlue);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.CullFace);
        GL.CullFace(CullFaceMode.Back);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);

        Renderer.Render(delta);

        Render?.Invoke(delta);

        GLDebug.TryCollectAllGLErrors("OnRender");
    }

    private void OnResize(Vector2D<int> size)
    {
        GL.Viewport(size);
    }

    private void OnFocusChanged(bool obj)
    {
        if (obj)
            Focused?.Invoke();
        else
            Unfocused?.Invoke();
    }
}
