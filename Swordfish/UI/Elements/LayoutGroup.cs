using ImGuiNET;

namespace Swordfish.UI.Elements;

public class LayoutGroup : AbstractPaneElement
{
    public LayoutGroup() : base(string.Empty) { }

    protected override void OnRender()
    {
        Constraints.Max = ImGui.GetContentRegionAvail();
        ImGui.SetCursorPos(ImGui.GetCursorPos() + Constraints.GetPosition());
        ImGui.BeginChild(UniqueName, Constraints.GetDimensions(), false, Flags | ImGuiWindowFlags.NoBackground);

        ImGui.Columns(Content.Count);
        base.OnRender();

        ImGui.EndChild();
    }

    protected override void RenderContentSeparator()
    {
        ImGui.NextColumn();
    }
}
