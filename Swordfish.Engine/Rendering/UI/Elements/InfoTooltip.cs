using ImGuiNET;
using Swordfish.Engine.Rendering.UI.Elements.Interfaces;

namespace Swordfish.Engine.Rendering.UI.Elements
{
    public class InfoTooltip : Tooltip
    {
        public override void OnShow()
        {
            ImGui.TextDisabled("?");
            
            base.OnShow();
        }
    }
}