using System.Numerics;

using ImGuiNET;

using Swordfish.Engine.Rendering.UI.Elements;

namespace Swordfish.Engine.Rendering.UI.Models
{
    public class Foldout : ContentGroupElement
    {
        public Vector2 Size;
        
        public bool TitleBar = true;

        public bool Border = true;

        public ImGuiTreeNodeFlags Flags;

        public Foldout() : base() {}

        public Foldout(string name) : base(name) {}
        
        public override void OnShow()
        {
            base.OnShow();

            if (ImGui.CollapsingHeader(ImGuiUniqueName, Flags))
            {
                base.TryShowTooltip();

                foreach (Element child in Content)
                {
                    child.OnShow();
                    base.TryInsertContentSeparator();
                }
            }
            else
            {
                base.TryShowTooltip();
            }
        }
    }
}
