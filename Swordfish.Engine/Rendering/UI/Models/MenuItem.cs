using System;
using System.Collections.Concurrent;
using System.Xml.Serialization;

using ImGuiNET;

using OpenTK.Windowing.GraphicsLibraryFramework;

using Swordfish.Engine.Rendering.UI.Elements;
using Swordfish.Library.Types;

namespace Swordfish.Engine.Rendering.UI.Models
{
    public class MenuItem : Element
    {
        public Shortcut Shortcut = new Shortcut {
            Modifiers = ShortcutModifiers.None,
            Key = Keys.Unknown
        };

        public bool Value;

        public LockedList<MenuItem> Items = new LockedList<MenuItem>();

        [XmlIgnore]
        public EventHandler<EventArgs> Clicked;

        [XmlIgnore]
        public EventHandler<EventArgs> Toggled;

        public MenuItem() : base() {}

        public MenuItem(string name) : base(name) {}

        public override void OnUpdate()
        {
            base.OnUpdate();

            if (Enabled && Items.Count == 0 && Shortcut.IsPressed())
                Clicked?.Invoke(this, EventArgs.Empty);
            
            Items.ForEach((item) => item.OnUpdate());
        }
        
        public override void OnShow()
        {
            base.OnShow();
            
            if (Items.Count == 0)
            {
                bool oldValue = Value;
                bool wasClicked;
                
                if (Clicked == null)
                    wasClicked = ImGui.MenuItem(ImGuiUniqueName, Shortcut.ToString(), ref Value, Enabled);
                else
                    wasClicked = ImGui.MenuItem(ImGuiUniqueName, Shortcut.ToString(), false, Enabled);

                if (wasClicked)
                {
                    base.TryShowTooltip();
                    Clicked?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    base.TryShowTooltip();

                    if (oldValue != Value)
                        Toggled?.Invoke(this, EventArgs.Empty);
                }
            }
            else
            {
                if (ImGui.BeginMenu(ImGuiUniqueName, Enabled))
                {
                    base.TryShowTooltip();

                    Items.ForEach((item) => item.OnShow());
                    
                    ImGui.EndMenu();
                }
                else
                {
                    base.TryShowTooltip();
                }
            }
        }
    }
}
