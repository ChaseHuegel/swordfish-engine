using System.Numerics;
using ImGuiNET;
using Swordfish.UI.ImGuiNET;

namespace Swordfish.UI.Elements;

public class PaneElement(in string? name) : AbstractPaneElement(name)
{
    // ReSharper disable once UnusedMember.Global
    public PaneElement() : this(null) { }

    protected override void OnRender()
    {
        //  base max/origin off the parent or current context
        Constraints.Max = (Parent as IConstraintsProperty)?.Constraints.Max ?? ImGui.GetContentRegionAvail();
        Vector2 origin = Alignment == ElementAlignment.NONE ? Vector2.Zero : ImGui.GetCursorPos();

        ImGui.SetCursorPos(origin + Constraints.GetPosition());

        ImGuiEx.BeginPane(Name, Constraints.GetDimensions(), true);

        Constraints.Max = Constraints.GetDimensions();
        base.OnRender();

        ImGuiEx.EndPane();

        TooltipProperty.RenderTooltip();
    }
}
