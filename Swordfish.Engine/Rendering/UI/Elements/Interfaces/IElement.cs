using Swordfish.Engine.Rendering.UI.Models;

namespace Swordfish.Engine.Rendering.UI.Elements.Interfaces
{
    public interface IElement
    {
        string Name { get; set; }

        bool Enabled { get; set; }

        void OnUpdate();

        void OnShow();

        void Destroy();
    }
}