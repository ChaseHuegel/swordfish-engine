using System.Collections.Concurrent;
using System.Collections.Generic;

using ImGuiNET;

using Swordfish.Engine.Rendering.UI.Models;
using Swordfish.Library.Types;

namespace Swordfish.Engine.Rendering.UI.Elements
{
    public class ContentGroupElement : Element
    {
        public ContentSeparator ContentSeparator;

        public LockedList<Element> Content = new LockedList<Element>();

        public ContentGroupElement() : base() {}

        public ContentGroupElement(string name) : base(name) {}
        
        public override void OnUpdate()
        {
            base.OnUpdate();

            Content.ForEach((item) => item.OnUpdate());
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
