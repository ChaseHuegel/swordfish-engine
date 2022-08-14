using System.Collections.Generic;
using System.Numerics;
using System.Xml.Serialization;

using ImGuiNET;

using Swordfish.Engine.Rendering.UI.Elements;

namespace Swordfish.Engine.Rendering.UI.Models
{
    public class TabMenuItem : ContentGroupElement
    {
        public TabMenuItem() : base() {}

        public TabMenuItem(string name) : base(name) {}
        
        public override void OnShow()
        {
            base.OnShow();
            
            if (ImGui.BeginTabItem(ImGuiUniqueName))
            {
                base.TryShowTooltip();
                ImGui.BeginChild(ImGuiUniqueName + "_Content");

                foreach (Element element in Content)
                {
                    element.OnShow();
                    base.TryInsertContentSeparator();
                }
                
                ImGui.EndChild();
                ImGui.EndTabItem();
            }
            else
            {
                base.TryShowTooltip();
            }
        }
    }
}
