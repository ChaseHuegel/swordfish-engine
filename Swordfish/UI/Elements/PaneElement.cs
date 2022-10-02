using System.Numerics;
using ImGuiNET;
using Swordfish.UI.ImGuiNET;

namespace Swordfish.UI.Elements;

public class PaneElement : AbstractPaneElement
{
    public PaneElement(string? name) : base(name) { }

    protected override void OnRender()
    {
        //  base max/origin off the parent or current context
        Constraints.Max = (Parent as IConstraintsProperty)?.Constraints.Max ?? ImGui.GetWindowSize();
        Vector2 origin = Alignment == ElementAlignment.NONE ? Vector2.Zero : ImGui.GetCursorPos();

        ImGui.SetCursorPos(origin + Constraints.GetPosition());

        ImGuiEx.BeginPane(Name, Constraints.GetDimensions(), true);

        base.OnRender();

        ImGuiEx.EndGroupPanel();

        TooltipProperty.RenderTooltip();
    }
}
