using System.Collections.Generic;
using System.Numerics;
using System.Xml.Serialization;

using ImGuiNET;

using Swordfish.Engine.Rendering.UI.Elements;

namespace Swordfish.Engine.Rendering.UI.Models
{
    public class Panel : ContentGroupElement
    {
        public Vector2 Size;
        
        public bool TitleBar = true;

        public bool Border = true;

        public ImGuiWindowFlags Flags;

        public Panel() : base() {}

        public Panel(string name) : base(name) {}
        
        public override void OnShow()
        {
            base.OnShow();

            ImGui.BeginChild(ImGuiUniqueName, Size, Border, Flags);

            if (TitleBar)
            {
                ImGui.Text(Name);
                base.TryShowTooltip();
                ImGui.Separator();
            }

            foreach (Element child in Content)
            {
                child.OnShow();
                base.TryInsertContentSeparator();
            }
            
            ImGui.EndChild();
        }
    }
}
