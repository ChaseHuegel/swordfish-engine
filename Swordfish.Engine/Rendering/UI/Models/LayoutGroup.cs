using System.Collections.Generic;
using System.Numerics;
using System.Xml.Serialization;

using ImGuiNET;

using Swordfish.Engine.Rendering.UI.Elements;

namespace Swordfish.Engine.Rendering.UI.Models
{
    public class LayoutGroup : Element
    {
        public List<Element> Content = new List<Element>();

        public LayoutGroup() : base() {}

        public LayoutGroup(string name) : base(name) {}
        
        public override void OnShow()
        {            
            ImGui.BeginGroup();

            foreach (Element element in Content)
            {
                element.Layout = Layout;                
                element.OnShow();
            }
            
            ImGui.EndGroup();
        }
    }
}
