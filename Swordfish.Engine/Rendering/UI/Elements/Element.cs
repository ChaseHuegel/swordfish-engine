using Swordfish.Engine.Rendering.UI.Elements.Interfaces;

namespace Swordfish.Engine.Rendering.UI.Elements
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
                Swordfish.Renderer.UiContext.Register(this);
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
                Swordfish.Renderer.UiContext.Unregister(this);
        }
    }
}