namespace Swordfish.UI.Elements;

public interface IElement : IUidProperty
{
    bool Enabled { get; set; }

    bool Visible { get; set; }

    void Render();
}
