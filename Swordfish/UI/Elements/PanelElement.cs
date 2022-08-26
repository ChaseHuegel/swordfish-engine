using System.Numerics;
using ImGuiNET;

namespace Swordfish.UI.Elements;

public class PanelElement : AbstractPaneElement
{
    public bool TitleBar { get; set; } = true;

    public bool Border { get; set; } = true;

    public PanelElement(string name) : base(name) { }

    protected override void OnRender()
    {
        Constraints.Max = ImGui.GetContentRegionAvail();
        ImGui.SetCursorPos(ImGui.GetCursorPos() + Constraints.GetPosition());
        ImGui.BeginChild(UniqueName, Constraints.GetDimensions(), Border, Flags);

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
