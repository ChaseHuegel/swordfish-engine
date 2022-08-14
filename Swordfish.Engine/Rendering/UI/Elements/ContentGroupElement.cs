using System.Collections.Generic;

using ImGuiNET;

using Swordfish.Engine.Rendering.UI.Models;

namespace Swordfish.Engine.Rendering.UI.Elements
{
    public class ContentGroupElement : Element
    {
        public ContentSeparator ContentSeparator;

        public List<Element> Content = new List<Element>();

        public ContentGroupElement() : base() {}

        public ContentGroupElement(string name) : base(name) {}
        
        public override void OnUpdate()
        {
            base.OnUpdate();

            foreach (Element element in Content)
                element.OnUpdate();
        }

        public void TryInsertContentSeparator()
        {
            switch (ContentSeparator)
            {
                case ContentSeparator.Divider:
                    ImGui.Separator();
                    break;
                case ContentSeparator.Spacer:
                    ImGui.Spacing();
                    break;
                default:
                    break;
            }
        }
    }
}
