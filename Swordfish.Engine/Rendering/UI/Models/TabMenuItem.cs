using System.Collections.Generic;
using System.Numerics;
using System.Xml.Serialization;

using ImGuiNET;

using Swordfish.Engine.Rendering.UI.Elements;

namespace Swordfish.Engine.Rendering.UI.Models
{
    public class TabMenuItem : Element
    {
        public List<Element> Content = new List<Element>();

        public TabMenuItem() : base() {}

        public TabMenuItem(string name) : base(name) {}
        
        public override void OnShow()
        {
            base.OnShow();
            
            if (ImGui.BeginTabItem(Name))
            {
                ImGui.BeginChild(Name + "_Content");

                foreach (Element element in Content)
                    element.OnShow();
                
                ImGui.EndChild();
                ImGui.EndTabItem();
            }
        }
    }
}
