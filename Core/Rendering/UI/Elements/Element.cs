using ImGuiNET;
using Swordfish.Core.Rendering.UI.Elements.Interfaces;
using Swordfish.Library.Diagnostics;

namespace Swordfish.Core.Rendering.UI.Elements
{
    public class Element : IElement
    {
        public bool Enabled { get; set; }

        public string Name { 
            get => string.IsNullOrEmpty(_Name) ? this.GetType().ToString() : _Name;
            set => _Name = value;
        }

        private string _Name;

        public Element()
        {
            if (!(this is IUnregistered))
                Engine.Renderer.UiContext.Register(this);
        }

        public virtual void OnUpdate()
        {

        }

        public virtual void OnShow()
        {
            
        }

        public virtual void Destroy()
        {
            if (!(this is IUnregistered))
                Engine.Renderer.UiContext.Unregister(this);
        }
    }
}