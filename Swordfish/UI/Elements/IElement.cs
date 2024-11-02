namespace Swordfish.UI.Elements;

public interface IElement : IUIDProperty
{
    bool Enabled { get; set; }

    bool Visible { get; set; }

    ElementAlignment Alignment { get; set; }

    IContentElement? Parent { get; set; }

    void Render();
}
