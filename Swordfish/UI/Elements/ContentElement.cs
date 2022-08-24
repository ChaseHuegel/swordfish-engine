using Swordfish.Library.Types;

namespace Swordfish.UI.Elements;

public abstract class ContentElement : Element, IContentElement
{
    public LockedList<IElement> Content { get; } = new();

    protected override void OnRender()
    {
        Content.ForEach((element) => element.Render());
    }
}
