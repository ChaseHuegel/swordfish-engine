using System;
using System.Collections.Generic;
using System.Xml.Serialization;

using ImGuiNET;

using OpenTK.Windowing.GraphicsLibraryFramework;

using Swordfish.Engine.Rendering.UI.Elements;
using Swordfish.Library.Diagnostics;

namespace Swordfish.Engine.Rendering.UI.Models
{
    public class MenuItem : Element
    {
        public Shortcut Shortcut = new Shortcut {
            Modifiers = ShortcutModifiers.None,
            Key = Keys.Unknown
        };

        public bool Value;

        public List<MenuItem> Items = new List<MenuItem>();

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
            
            foreach (MenuItem item in Items)
                item.OnUpdate();
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

                    foreach (MenuItem item in Items)
                        item.OnShow();
                    
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
