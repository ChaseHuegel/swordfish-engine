using ImGuiNET;

using Swordfish.Engine.Rendering.UI.Elements;
using Swordfish.Engine.Types;

namespace Swordfish.Engine.Rendering.UI.Models
{
    public class Text : Element
    {
        public string Value;

        public bool Wrap = true;
        
        public string Label;

        public Color Color = Color.White;

        public Text() {}

        public Text(string value)
        {
            Value = value;
        }

        public Text(string value, bool wrap)
        {
            Value = value;
            Wrap = wrap;
        }

        public override void OnShow()
        {
            base.OnShow();
            
            if (Wrap) ImGui.PushTextWrapPos();
            ImGui.PushStyleColor(ImGuiCol.Text, Enabled ? Color : Color.Gray);
            
            if (string.IsNullOrWhiteSpace(Label))
            {
                if (Value.StartsWith('-'))
                    ImGui.BulletText(Value.TrimStart('-', ' '));
                else
                    ImGui.TextUnformatted(Value);
            }
            else
                ImGui.LabelText(Label, Value);

            ImGui.PopStyleColor();
            if (Wrap) ImGui.PopTextWrapPos();
            base.TryShowTooltip();
        }
    }
}
