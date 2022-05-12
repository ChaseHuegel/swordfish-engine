using ImGuiNET;
using Swordfish.Engine.Rendering.UI.Elements.Interfaces;

namespace Swordfish.Engine.Rendering.UI.Elements
{
    public class Tooltip : Element, ITextElement, IUnregistered
    {
        public string Text { get; set; }

        public override void OnShow()
        {
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                    ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35f);
                    ImGui.TextUnformatted(Text);
                    ImGui.PopTextWrapPos();                
                ImGui.EndTooltip();
            }
        }
    }
}