using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Types;
using Swordfish.UI.Elements;

namespace Swordfish.UI;

public class ImGuiContext : IUIContext
{
    private ImGuiController Controller => controller ??= new ImGuiController(GL.GetApi(Window), Window, Window?.CreateInput());
    private ImGuiController? controller;

    public LockedList<IElement> Elements { get; } = new();

    private IWindow? Window { get; set; }

    public void Initialize(IWindow window)
    {
        Window = window;
        Window.Closing += Cleanup;
        Window.Render += Render;

        Debugger.Log("UI initialized.");
    }

    private void Cleanup()
    {
        Controller.Dispose();
    }

    private void Render(double delta)
    {
        Controller.Update((float)delta);

        foreach (IElement element in Elements)
            element.Render();

        Controller.Render();
    }

}
