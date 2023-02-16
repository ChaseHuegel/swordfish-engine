using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using Swordfish.Library.Collections;
using Swordfish.Library.Constraints;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Types;
using Swordfish.UI.Elements;

namespace Swordfish.UI;

public class ImGuiContext : IUIContext
{
    private GL GL => gl ??= SwordfishEngine.Kernel.Get<GL>();
    private GL gl;

    public LockedList<IElement> Elements { get; } = new();
    public IMenuBarElement? MenuBar { get; set; }

    public DataBinding<IConstraint> ScaleConstraint { get; set; } = new DataBinding<IConstraint>(new AbsoluteConstraint(1f));
    public DataBinding<float> FontScale { get; } = new DataBinding<float>(1f);
    public DataBinding<float> FontDisplaySize { get; } = new DataBinding<float>();

    private DataBinding<float> Scale { get; } = new DataBinding<float>(1f);
    private ImGuiController? Controller { get; set; }
    private IWindow? Window { get; set; }

    public void Initialize(IWindow window, IInputContext inputContext)
    {
        Window = window;
        Window.Closing += Cleanup;
        Window.Render += Render;

        Scale.Changed += OnFontScaleChanged;
        FontScale.Changed += OnFontScaleChanged;
        ScaleConstraint.Changed += OnScalingConstraintChanged;

        Controller = new ImGuiController(GL, Window, inputContext);
        Controller.Update(0f);

        OnFontScaleChanged(this, EventArgs.Empty);

        Debugger.Log("UI initialized.");
        Debugger.Log($"using ImGui {ImGui.GetVersion()}", LogType.CONTINUED);
    }

    private void OnScalingConstraintChanged(object? sender, EventArgs e)
    {
        Scale.Set(ScaleConstraint.Get().GetValue(Window?.Monitor?.VideoMode.Resolution?.Y ?? 1f));
    }

    private void OnFontScaleChanged(object? sender, EventArgs e)
    {
        ImGui.GetIO().FontGlobalScale = FontScale.Get() * Scale.Get();
        FontDisplaySize.Set(ImGui.GetFontSize() * FontScale.Get() * Scale.Get());
    }

    private void Cleanup()
    {
        Controller?.Dispose();
    }

    private void Render(double delta)
    {
        Controller?.Update((float)delta);

        foreach (IElement element in Elements)
            element.Render();

        MenuBar?.Render();
        Controller?.Render();
    }

}
