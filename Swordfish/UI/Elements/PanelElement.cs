using System.Numerics;
using ImGuiNET;

namespace Swordfish.UI.Elements;

public class PanelElement : AbstractPaneElement
{
    public bool TitleBar { get; set; } = true;

    public bool Border { get; set; } = true;

    public PanelElement(string? name) : base(name) { }

    protected override void OnRender()
    {
        //  base max/origin off the parent or current context
        Constraints.Max = (Parent as IConstraintsProperty)?.Constraints.Max ?? ImGui.GetContentRegionAvail();
        Vector2 origin = Alignment == ElementAlignment.NONE ? Vector2.Zero : ImGui.GetCursorPos();

        ImGui.SetCursorPos(origin + Constraints.GetPosition());

        ImGui.BeginChild(UniqueName, Constraints.GetDimensions(), Border ? ImGuiChildFlags.Border : ImGuiChildFlags.None);

        if (TitleBar)
        {
            ImGui.Text(Name);
            TooltipProperty.RenderTooltip();
            ImGui.Separator();
        }

        base.OnRender();

        ImGui.EndChild();
    }
}
