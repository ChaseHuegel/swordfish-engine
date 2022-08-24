namespace Swordfish.UI.Elements;

public interface IElement
{
    bool Enabled { get; set; }

    bool Visible { get; set; }

    void Render();
}
