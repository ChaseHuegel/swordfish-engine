using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using Swordfish.Library.Diagnostics;

namespace Swordfish.UI;

public class ImGuiContext : IUIContext
{
    private IWindow? Window { get; set; }

    private ImGuiController Controller => controller ??= new ImGuiController(GL.GetApi(Window), Window, Window?.CreateInput());
    private ImGuiController? controller;

    public void Initialize(IWindow window)
    {
        Window = window;
        Window.Closing += Cleanup;
        Window.Render += Render;

        Debug.Log("UI initialized.");
    }

    private void Cleanup()
    {
        Controller.Dispose();
    }

    private void Render(double delta)
    {
        Controller.Update((float)delta);

        ImGuiNET.ImGui.ShowDemoWindow();

        Controller.Render();
    }

}
