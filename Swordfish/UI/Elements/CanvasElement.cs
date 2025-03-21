using System.Numerics;
using ImGuiNET;
using Swordfish.Graphics;

namespace Swordfish.UI.Elements;

public class CanvasElement : AbstractPaneElement
{
    protected readonly IWindowContext WindowContext;

    public bool Open { get; set; } = true;

    public CanvasElement(IUIContext uiContext, IWindowContext windowContext, string name) : base(name)
    {
        UIContext = uiContext;
        WindowContext = windowContext;
        UIContext.Add(this);
    }

    protected override void OnRender()
    {
        //  Based max/origin off a parent or the screen
        Vector2 anchor = ImGui.GetCursorPos() - ImGui.GetStyle().WindowPadding;
        Constraints.Max = (Parent as IConstraintsProperty)?.Constraints.Max ?? WindowContext.GetSize() - anchor;
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
