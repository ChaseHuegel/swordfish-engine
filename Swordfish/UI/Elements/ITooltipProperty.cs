using ImGuiNET;
using Swordfish.Integrations;

namespace Swordfish.UI.Elements;

public interface ITooltipProperty
{
    Tooltip Tooltip { get; set; }

    void RenderTooltip()
    {
        if (string.IsNullOrWhiteSpace(Tooltip.Text))
            return;

        if (Tooltip.Help)
        {
            ImGui.SameLine();
            ImGui.TextDisabled(FontAwesome.CircleQuestion);
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(Tooltip.MaxWidth > 0 ? Tooltip.MaxWidth : ImGui.GetFontSize() * 16);
            ImGui.TextUnformatted(Tooltip.Text);
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }
    }
}
