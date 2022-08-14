using ImGuiNET;

using Swordfish.Engine.Rendering.UI.Elements;

namespace Swordfish.Engine.Rendering.UI.Models
{
    public class Checkbox : Element
    {
        public bool Checked;

        public Checkbox() {}

        public Checkbox(string name) : base(name) {}

        public override void OnShow()
        {
            base.OnShow();
            ImGui.Checkbox(ImGuiUniqueName, ref Checked);
            base.TryShowTooltip();
        }
    }
}
