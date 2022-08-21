using System.Collections.Concurrent;
using ImGuiNET;

using Swordfish.Engine.Rendering.UI.Elements;
using Swordfish.Library.Types;

namespace Swordfish.Engine.Rendering.UI.Models
{
    public class Menu : Element
    {
        public LockedList<MenuItem> Items = new LockedList<MenuItem>();

        public Menu() {}

        public Menu(string name) : base(name) {}

        public override void OnUpdate()
        {
            base.OnUpdate();

            Items.ForEach((item) => item.OnUpdate());
        }

        public override void OnShow()
        {
            base.OnShow();
            
            if (ImGui.BeginMenuBar())
            {
                Items.ForEach((item) => item.OnShow());
                
                ImGui.EndMenuBar();
            }
        }
    }
}
