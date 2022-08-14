using System.Xml.Serialization;
using System;
using System.Numerics;

using ImGuiNET;

using Swordfish.Engine.Rendering.UI.Elements;

namespace Swordfish.Engine.Rendering.UI.Models
{
    public class Button : Element
    {
        public Vector2 Size;

        [XmlIgnore]
        public EventHandler<EventArgs> Clicked;

        public Button() {}

        public Button(string name) : base(name) {}

        public override void OnShow()
        {
            base.OnShow();

            if (ImGui.Button(ImGuiUniqueName, Size))
            {
                base.TryShowTooltip();
                Clicked?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                base.TryShowTooltip();
            }
        }
    }
}
