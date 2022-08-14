using System;
using System.Drawing;
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
    [XmlInclude(typeof(Checkbox))]
    [XmlInclude(typeof(CheckboxFlags))]
    [XmlInclude(typeof(Button))]
    [XmlInclude(typeof(Menu))]
    [XmlInclude(typeof(MenuItem))]
    [XmlInclude(typeof(Spacer))]
    public class Canvas : ContentGroupElement
    {
        public bool TryLoadLayout = true;

        public Vector2 Position;

        public Vector2 Size;

        public SizeBehavior SizeBehavior;
        
        public bool TitleBar = true;

        public bool Border = true;

        public ImGuiWindowFlags Flags;

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

            switch (SizeBehavior)
            {
                case SizeBehavior.Absolute:
                    ImGui.SetNextWindowSize(Size, TryLoadLayout ? ImGuiCond.FirstUseEver : ImGuiCond.Once);
                    break;
                case SizeBehavior.Relative:
                    ImGui.SetNextWindowSize(new Vector2(
                        Swordfish.Settings.Window.WIDTH * Size.X,
                        Swordfish.Settings.Window.HEIGHT * Size.Y
                    ));
                    break;
                default:
                    throw new NotImplementedException();
            }

            ImGui.Begin(ImGuiUniqueName, Flags);
            base.TryShowTooltip();

            foreach (Element element in Content)
            {
                element.OnShow();
                base.TryInsertContentSeparator();
            }
            
            ImGui.End();
        }
    }
}
