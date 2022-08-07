using System.Collections.Generic;
using System.Numerics;
using System.Xml.Serialization;

using ImGuiNET;

using Swordfish.Engine.Rendering.UI.Elements;

namespace Swordfish.Engine.Rendering.UI.Models
{
    public class ScrollView : Element
    {
        public Vector2 Size;

        public bool Border = true;

        public ImGuiWindowFlags Flags;

        public List<Element> Content = new List<Element>();

        public ScrollView() : base() {}
        
        public override void OnShow()
        {
            base.OnShow();

            ImGui.BeginChild("ScrollView", Size, Border, Flags | ImGuiWindowFlags.HorizontalScrollbar);

            foreach (Element child in Content)
                child.OnShow();
            
            ImGui.EndChild();
        }
    }
}
