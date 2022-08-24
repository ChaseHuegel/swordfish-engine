using Swordfish.Library.Types;

namespace Swordfish.UI.Elements;

public interface IContentElement : IElement
{
    LockedList<IElement> Content { get; }
}
