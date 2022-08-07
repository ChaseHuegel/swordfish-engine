using ImGuiNET;

using Swordfish.Engine.Rendering.UI.Elements;

namespace Swordfish.Engine.Rendering.UI.Models
{
    public class Divider : Element
    {
        public Divider() {}

        public override void OnShow()
        {
            base.OnShow();
            ImGui.Separator();
        }
    }
}
