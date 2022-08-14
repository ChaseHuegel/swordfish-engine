using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using Swordfish.Engine.Rendering.UI.Elements;

namespace Swordfish.Engine.Rendering.UI.Models
{
    public class TreeView : Element
    {
        public Vector2 Size;
        
        public bool TitleBar = true;

        public bool Border = true;

        public ImGuiTreeNodeFlags Flags;

        public List<TreeViewNode> Nodes = new List<TreeViewNode>();

        public TreeView() : base() {}

        public TreeView(string name) : base(name) {}
        
        public override void OnShow()
        {
            base.OnShow();

            lock (Nodes)
            {
                foreach (TreeViewNode node in Nodes)
                    RecursiveOnShow(node);

                void RecursiveOnShow(TreeViewNode node)
                {                
                    if (ImGui.TreeNodeEx($"{node.Name}##{node.Uid}", node.Nodes?.Count > 0 ? Flags : Flags | ImGuiTreeNodeFlags.Leaf))
                    {
                        if (node.Nodes?.Count > 0)
                        {
                            foreach (TreeViewNode child in node.Nodes)
                                RecursiveOnShow(child);
                        }
                        
                        ImGui.TreePop();
                    }
                }
            }
        }
    }
}
