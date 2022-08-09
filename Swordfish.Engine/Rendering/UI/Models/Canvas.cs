using System.Collections.Generic;
using System.Numerics;
using System.Xml.Serialization;

using ImGuiNET;

using Swordfish.Engine.Rendering.UI.Elements;

namespace Swordfish.Engine.Rendering.UI.Models
{
    [XmlInclude(typeof(Panel))]
    [XmlInclude(typeof(Group))]
    [XmlInclude(typeof(LayoutGroup))]
    [XmlInclude(typeof(Text))]
    [XmlInclude(typeof(Divider))]
    [XmlInclude(typeof(TabMenu))]
    [XmlInclude(typeof(TabMenuItem))]
    [XmlInclude(typeof(ScrollView))]
    [XmlInclude(typeof(Foldout))]
    [XmlInclude(typeof(TreeNode))]
    public class Canvas : Element
    {
        public bool TryLoadLayout = true;

        public Vector2 Position;

        public Vector2 Size;
        
        public bool TitleBar = true;

        public bool Border = true;

        public ImGuiWindowFlags Flags;

        public List<Element> Content = new List<Element>();

        public Canvas() : base()
        {
            Initialize();
        }

        public Canvas(string name) : base(name)
        {
            Initialize();
        }
        
        public override void OnShow()
        {
            ImGui.SetNextWindowPos(Position, TryLoadLayout ? ImGuiCond.FirstUseEver : ImGuiCond.Once);
            ImGui.SetNextWindowSize(Size, TryLoadLayout ? ImGuiCond.FirstUseEver : ImGuiCond.Once);

            ImGui.Begin(ImGuiUniqueName, Flags);
            base.TryShowTooltip();

            foreach (Element element in Content)
                element.OnShow();
            
            ImGui.End();
        }
    }
}
