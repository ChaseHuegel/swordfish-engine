using System.Collections.Concurrent;
using System.Numerics;

using ImGuiNET;

using Swordfish.Engine.Rendering.UI.Elements;
using Swordfish.Library.Types;

namespace Swordfish.Engine.Rendering.UI.Models
{
    public class TabMenu : Element
    {
        public Vector2 Size;

        public ImGuiTabBarFlags Flags;

        public LockedList<TabMenuItem> Items = new LockedList<TabMenuItem>();

        public TabMenu() {}

        public TabMenu(string name) : base(name) {}

        public override void OnUpdate()
        {
            base.OnUpdate();

            Items.ForEach((item) => item.OnUpdate());
        }

        public override void OnShow()
        {
            base.OnShow();
            
            ImGui.BeginChild(ImGuiUniqueName, Size, false, ImGuiWindowFlags.None);
            ImGui.BeginTabBar(ImGuiUniqueName + "_TabBar", Flags);

            Items.ForEach((item) => item.OnShow());
            
            ImGui.EndTabBar();
            ImGui.EndChild();
        }
    }
}
