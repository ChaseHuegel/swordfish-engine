using ImGuiNET;

using Swordfish.Engine.Rendering.UI.Elements;

namespace Swordfish.Engine.Rendering.UI.Models
{
    public class Label : Element
    {
        public string Text;

        public bool Wrap = true;

        public Label() {}

        public Label(string text)
        {
            Text = text;
        }

        public Label(string text, bool wrap)
        {
            Text = text;
            Wrap = wrap;
        }

        public override void OnShow()
        {
            base.OnShow();
            
            if (Wrap)
                ImGui.TextWrapped(Text);
            else
                ImGui.Text(Text);
        }
    }
}
