using Swordfish.Library.Types;

namespace Swordfish.UI.Elements;

public interface IContentElement : IElement
{
    ContentSeparator ContentSeparator { get; set; }

    LockedList<IElement> Content { get; }
}
