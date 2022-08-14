using System.Collections.Generic;
using System.Numerics;
using System.Xml.Serialization;

using ImGuiNET;

using Swordfish.Engine.Rendering.UI.Elements;

namespace Swordfish.Engine.Rendering.UI.Models
{
    public struct TreeViewNode
    {
        public string Name;

        public int Uid;

        public List<TreeViewNode> Nodes;
    }
}
