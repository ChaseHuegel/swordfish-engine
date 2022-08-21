using System.Collections.Concurrent;
using Swordfish.Library.Types;

namespace Swordfish.Engine.Rendering.UI.Models
{
    public struct TreeViewNode
    {
        public string Name;

        public int Uid;

        public LockedList<TreeViewNode> Nodes;

        public TreeViewNode(string name)
        {
            Name = name;
            Uid = 0;
            Nodes = new LockedList<TreeViewNode>();
        }
    }
}
