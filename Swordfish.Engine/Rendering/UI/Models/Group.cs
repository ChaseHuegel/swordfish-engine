using System.Collections.Generic;
using System.Numerics;
using System.Xml.Serialization;

using ImGuiNET;

using Swordfish.Engine.Rendering.UI.Elements;

namespace Swordfish.Engine.Rendering.UI.Models
{
    public class Group : Element
    {
        public List<Element> Content = new List<Element>();

        public Group() : base() {}

        public Group(string name) : base(name) {}
        
        public override void OnShow()
        {
            base.OnShow();

            ImGui.BeginGroup();

            foreach (Element element in Content)
                element.OnShow();
            
            ImGui.EndGroup();
            base.TryShowTooltip();
        }
    }
}
