using ImGuiNET;
using Swordfish.Core.Rendering.UI.Elements.Interfaces;

namespace Swordfish.Core.Rendering.UI.Elements
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