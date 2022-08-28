using System.Numerics;
using ImGuiNET;
using Ninject;

namespace Swordfish.UI.Elements;

public class CanvasElement : AbstractPaneElement
{
    private IUIContext UIContext => uiContext ??= SwordfishEngine.Kernel.Get<IUIContext>();
    private IUIContext? uiContext;

    public bool Open { get; set; } = true;

    public CanvasElement(string name) : base(name)
    {
        UIContext.Elements.Add(this);
    }

    protected override void OnRender()
    {
        //  Based max/origin off a parent or the screen
        Vector2 anchor = ImGui.GetCursorPos() - ImGui.GetStyle().WindowPadding;
        Constraints.Max = (Parent as IConstraintsProperty)?.Constraints.Max ?? SwordfishEngine.MainWindow.GetSize() - anchor;
        Vector2 origin = (Parent as IConstraintsProperty)?.Constraints.GetPosition() ?? anchor;

        ImGui.SetNextWindowPos(origin + Constraints.GetPosition(), Flags.HasFlag(ImGuiWindowFlags.NoMove) ? ImGuiCond.Always : ImGuiCond.Once);
        ImGui.SetNextWindowSize(Constraints.GetDimensions(), Flags.HasFlag(ImGuiWindowFlags.AlwaysAutoResize) ? ImGuiCond.Always : ImGuiCond.Once);

        ImGui.SetNextWindowCollapsed(!Open);
        ImGui.Begin(UniqueName, Flags);
        Open = !ImGui.IsWindowCollapsed();

        TooltipProperty.RenderTooltip();

        base.OnRender();

        ImGui.End();
    }
}
