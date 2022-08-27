using System.Numerics;
using ImGuiNET;

namespace Swordfish.UI.Elements;

public class LayoutGroup : AbstractPaneElement
{
    public ElementAlignment Layout { get; set; }

    public LayoutGroup() : base(string.Empty) { }

    protected override void OnRender()
    {
        //  base max/origin off the parent or current context
        Constraints.Max = (Parent as IConstraintsProperty)?.Constraints.Max ?? ImGui.GetContentRegionMax();
        Vector2 origin = Alignment == ElementAlignment.NONE ? Vector2.Zero : ImGui.GetCursorPos();

        ImGui.SetCursorPos(origin + Constraints.GetPosition());
        ImGui.BeginChild(UniqueName, Constraints.GetDimensions(), false, Flags | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);

        if (Layout == ElementAlignment.HORIZONTAL && ContentSeparator != ContentSeparator.NONE)
            ImGui.Columns(Content.Count, UniqueName + "_col", ContentSeparator == ContentSeparator.DIVIDER);

        base.OnRender();

        ImGui.EndChild();
    }

    protected override void RenderContentSeparator()
    {
        if (Layout == ElementAlignment.HORIZONTAL)
        {
            if (ContentSeparator == ContentSeparator.NONE)
                ImGui.SameLine();
            else
                ImGui.NextColumn();
        }
        else
        {
            base.RenderContentSeparator();
        }
    }
}
