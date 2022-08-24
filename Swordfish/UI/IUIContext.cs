using Silk.NET.Windowing;
using Swordfish.Library.Types;
using Swordfish.UI.Elements;

namespace Swordfish.UI;

public interface IUIContext
{
    LockedList<IElement> Elements { get; }

    void Initialize(IWindow window);
}
