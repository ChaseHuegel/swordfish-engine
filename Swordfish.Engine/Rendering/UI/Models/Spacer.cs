using ImGuiNET;

using Swordfish.Engine.Rendering.UI.Elements;

namespace Swordfish.Engine.Rendering.UI.Models
{
    public class Spacer : Element
    {
        public Spacer() {}

        public override void OnShow()
        {
            base.OnShow();
            ImGui.Spacing();
        }
    }
}
