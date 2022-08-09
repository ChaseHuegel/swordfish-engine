using System.Collections.Generic;
using Swordfish.Engine.Rendering.UI.Elements.Interfaces;

namespace Swordfish.Engine.Rendering.UI
{
    public class UiContext
    {
        private HashSet<IElement> elements = new HashSet<IElement>(); 

        public bool Register(IElement element) => elements.Add(element);

        public bool Unregister(IElement element) {
            element.Destroy();

            return elements.Remove(element);
        }
        
        internal void Render()
        {
            foreach (IElement element in elements)
            {                
                if (element.Visible)
                {
                    element.OnUpdate();
                    element.OnShow();
                }
            }
        }
    }
}