using System.Collections.Generic;
using ImGuiNET;

using Swordfish.Engine.Rendering.UI.Elements;

namespace Swordfish.Engine.Rendering.UI.Models
{
    public class Menu : Element
    {
        public List<MenuItem> Items = new List<MenuItem>();

        public Menu() {}

        public Menu(string name) : base(name) {}

        public override void OnUpdate()
        {
            base.OnUpdate();

            foreach (MenuItem item in Items)
                item.OnUpdate();
        }

        public override void OnShow()
        {
            base.OnShow();
            
            if (ImGui.BeginMenuBar())
            {
                foreach (MenuItem item in Items)
                    item.OnShow();
                
                ImGui.EndMenuBar();
            }
        }
    }
}
