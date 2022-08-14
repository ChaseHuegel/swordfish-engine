using System.Numerics;
using System.Collections.Generic;
using ImGuiNET;

using Swordfish.Engine.Rendering.UI.Elements;

namespace Swordfish.Engine.Rendering.UI.Models
{
    public class TabMenu : Element
    {
        public Vector2 Size;

        public ImGuiTabBarFlags Flags;

        public List<TabMenuItem> Items = new List<TabMenuItem>();

        public TabMenu() {}

        public TabMenu(string name) : base(name) {}

        public override void OnUpdate()
        {
            base.OnUpdate();

            foreach (Element element in Items)
                element.OnUpdate();
        }

        public override void OnShow()
        {
            base.OnShow();
            
            ImGui.BeginChild(ImGuiUniqueName, Size, false, ImGuiWindowFlags.None);
            ImGui.BeginTabBar(ImGuiUniqueName + "_TabBar", Flags);

            foreach (TabMenuItem item in Items)
                item.OnShow();
            
            ImGui.EndTabBar();
            ImGui.EndChild();
        }
    }
}
