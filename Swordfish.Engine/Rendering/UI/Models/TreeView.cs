using System.Collections.Concurrent;
using System.Numerics;

using ImGuiNET;

using Swordfish.Engine.Rendering.UI.Elements;
using Swordfish.Library.Types;

namespace Swordfish.Engine.Rendering.UI.Models
{
    public class TreeView : Element
    {
        public Vector2 Size;
        
        public bool TitleBar = true;

        public bool Border = true;

        public ImGuiTreeNodeFlags Flags;

        public LockedList<TreeViewNode> Nodes = new LockedList<TreeViewNode>();

        public TreeView() : base() {}

        public TreeView(string name) : base(name) {}
        
        public override void OnShow()
        {
            base.OnShow();

            Nodes.ForEach((item) => RecursiveOnShow(item));

            void RecursiveOnShow(TreeViewNode node)
            {                
                if (ImGui.TreeNodeEx($"{node.Name}##{node.Uid}", node.Nodes?.Count > 0 ? Flags : Flags | ImGuiTreeNodeFlags.Leaf))
                {
                    if (node.Nodes?.Count > 0)
                        node.Nodes.ForEach((item) => RecursiveOnShow(item));
                    
                    ImGui.TreePop();
                }
            }
        }
    }
}
