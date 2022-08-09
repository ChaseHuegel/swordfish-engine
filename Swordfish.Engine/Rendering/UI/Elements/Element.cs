using ImGuiNET;

using Swordfish.Engine.Rendering.UI.Elements.Interfaces;
using Swordfish.Engine.Rendering.UI.Models;
using Swordfish.Integrations;

namespace Swordfish.Engine.Rendering.UI.Elements
{
    public class Element : IElement
    {
        public string Name { 
            get => string.IsNullOrWhiteSpace(name) ? this.GetType().ToString() : name;
            set {
                name = value;
                imGuidId = null;
            }
        }

        public string UID {
            get => string.IsNullOrWhiteSpace(uid) ? Swordfish.Random.Next().ToString() : uid;
            set {
                uid = value;
                imGuidId = null;
            }
        }
        
        public bool Visible { get; set; } = true;

        public bool Enabled { get; set; } = true;

        public Layout Alignment;

        public Tooltip Tooltip;

        internal string ImGuiUniqueName {
            get => imGuidId ?? (imGuidId = $"{Name}##{UID}");
        }

        private string imGuidId;
        private string uid;
        private string name;
        private bool initialized;

        public Element() {}

        public Element(string name)
        {
            Name = name;
        }

        public void Initialize()
        {
            if (!initialized)
            {
                Swordfish.Renderer.UiContext.Register(this);
                initialized = true;
            }
        }

        public void Destroy()
        {
            if (initialized)
                Swordfish.Renderer.UiContext.Unregister(this);
        }

        public virtual void OnUpdate()
        {

        }

        public virtual void OnShow()
        {
            if (Alignment == Layout.Horizontal)
                ImGui.SameLine();
        }

        protected void TryShowTooltip()
        {
            if (string.IsNullOrWhiteSpace(Tooltip.Text))
                return;
            
            if (Tooltip.Help)
            {
                ImGui.SameLine();
                ImGui.TextDisabled(FontAwesome.CircleQuestion);
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.PushTextWrapPos(Tooltip.MaxWidth > 0 ? Tooltip.MaxWidth : ImGui.GetFontSize() * 16);
                ImGui.TextUnformatted(Tooltip.Text);
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }
        }
    }
}