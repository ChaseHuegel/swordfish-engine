using System.Collections.Generic;
using System.Numerics;
using System.Xml.Serialization;

using ImGuiNET;

using Swordfish.Engine.Rendering.UI.Elements;

namespace Swordfish.Engine.Rendering.UI.Models
{
    public class TreeNode : Element
    {
        public Vector2 Size;
        
        public bool TitleBar = true;

        public bool Border = true;

        public ImGuiTreeNodeFlags Flags;

        public List<TreeNode> Nodes = new List<TreeNode>();

        public TreeNode() : base() {}

        public TreeNode(string name) : base(name) {}

        public override void OnUpdate()
        {
            base.OnUpdate();

            foreach (TreeNode node in Nodes)
                node.OnUpdate();
        }
        
        public override void OnShow()
        {
            base.OnShow();

            RecursiveOnShow(this);

            void RecursiveOnShow(TreeNode node)
            {                
                if (ImGui.TreeNodeEx(node.ImGuiUniqueName, node.Nodes.Count > 0 ? Flags : Flags | ImGuiTreeNodeFlags.Leaf))
                {                    
                    foreach (TreeNode child in node.Nodes)
                        RecursiveOnShow(child);
                    
                    ImGui.TreePop();
                }
            }
        }
    }
}
