using System.Collections.Generic;
using System.Numerics;
using System.Xml.Serialization;

using ImGuiNET;

using Swordfish.Engine.Rendering.UI.Elements;

namespace Swordfish.Engine.Rendering.UI.Models
{
    public class ScrollView : ContentGroupElement
    {
        public Vector2 Size;

        public bool Border = true;

        public ImGuiWindowFlags Flags;

        public ScrollView() : base() {}
        
        public override void OnShow()
        {
            base.OnShow();

            ImGui.BeginChild(ImGuiUniqueName, Size, Border, Flags | ImGuiWindowFlags.HorizontalScrollbar);

            foreach (Element child in Content)
            {
                child.OnShow();
                base.TryInsertContentSeparator();
            }
            
            ImGui.EndChild();
            base.TryShowTooltip();
        }
    }
}
