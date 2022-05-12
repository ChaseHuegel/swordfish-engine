namespace Swordfish.Engine.Rendering.UI.Elements.Interfaces
{
    public interface IElement
    {
        bool Enabled { get; set; }

        string Name { get; set; }

        void OnUpdate();

        void OnShow();

        void Destroy();
    }
}