using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using Ninject;
using Silk.NET.Core;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.IO;
using Swordfish.UI;
using Swordfish.Util;
using Image = SixLabors.ImageSharp.Image;

namespace Swordfish.Graphics;

public class SilkWindowContext : IWindowContext
{
    private IWindow Window { get; }
    private IRenderContext Renderer { get; }
    private IUIContext UIContext { get; }
    private IInputContext? Input { get; set; }

    public Action? Load { get; set; }
    public Action? Close { get; set; }
    public Action<double>? Render { get; set; }
    public Action<double>? Update { get; set; }

    public SilkWindowContext(IRenderContext renderer, IUIContext uiContext)
    {
        Renderer = renderer;
        UIContext = uiContext;

        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(800, 600);
        options.Title = "Swordfish";
        options.ShouldSwapAutomatically = true;

        Window = Silk.NET.Windowing.Window.Create(options);

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
        RawImage icon = Imaging.LoadIcon(pathService.Root.At("swordfish.ico"));
        Window.SetWindowIcon(ref icon);

        Input = Window.CreateInput();
        for (int i = 0; i < Input.Keyboards.Count; i++)
            Input.Keyboards[i].KeyDown += KeyDown;

        Renderer.Initialize(Window);
        UIContext.Initialize(Window);

        Load?.Invoke();
    }

    private void OnClose()
    {
        Input?.Dispose();

        Close?.Invoke();
    }

    private void OnUpdate(double delta)
    {
        Update?.Invoke(delta);
    }

    private void OnRender(double delta)
    {
        Render?.Invoke(delta);
    }

    private void KeyDown(IKeyboard keyboard, Key key, int value)
    {
        switch (key)
        {
            case Key.Escape:
                Window.Close();
                break;
            case Key.F11:
                Window.WindowState = Window.WindowState == WindowState.Normal ? WindowState.Fullscreen : WindowState.Normal;
                break;
        }
    }
}
