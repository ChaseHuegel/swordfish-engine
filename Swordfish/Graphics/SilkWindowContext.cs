using System.Numerics;
using Ninject;
using Silk.NET.Core;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Swordfish.Input;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.IO;
using Swordfish.UI;
using Swordfish.Util;
using Key = Swordfish.Library.IO.Key;

namespace Swordfish.Graphics;

public class SilkWindowContext : IWindowContext
{
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
        Debugger.Log("Window initialized.");

        Window.Center();

        var pathService = SwordfishEngine.Kernel.Get<IPathService>();
        RawImage icon = Imaging.LoadAsPng(pathService.Root.At("swordfish.ico"));
        Window.SetWindowIcon(ref icon);

        IInputContext input = Window.CreateInput();

        Renderer.Initialize(Window);
        UIContext.Initialize(Window, input);
        InputService.Initialize(input);

        ShortcutService.RegisterShortcut(new Shortcut(
                "Toggle Fullscreen",
                "UI",
                ShortcutModifiers.NONE,
                Key.F11,
                Shortcut.DefaultEnabled,
                () => Window.WindowState = Window.WindowState == WindowState.Normal ? WindowState.Fullscreen : WindowState.Normal
            )
        );

        Loaded?.Invoke();
    }

    private void OnClose()
    {
        Closed?.Invoke();
    }

    private void OnUpdate(double delta)
    {
        Update?.Invoke(delta);
    }

    private void OnRender(double delta)
    {
        Render?.Invoke(delta);
    }

    private void OnFocusChanged(bool obj)
    {
        if (obj)
            Focused?.Invoke();
        else
            Unfocused?.Invoke();
    }
}
