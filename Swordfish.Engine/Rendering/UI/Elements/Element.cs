using ImGuiNET;

using Swordfish.Engine.Rendering.UI.Elements.Interfaces;
using Swordfish.Engine.Rendering.UI.Models;

namespace Swordfish.Engine.Rendering.UI.Elements
{
    public class Element : IElement
    {
        public string Name { 
            get => string.IsNullOrEmpty(name) ? this.GetType().ToString() : name;
            set => name = value;
        }
        
        public bool Enabled { get; set; } = true;

        public Layout Layout;

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
            if (Layout == Layout.Horizontal)
                ImGui.SameLine();
        }
    }
}