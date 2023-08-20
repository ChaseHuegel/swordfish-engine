using Silk.NET.Windowing;
using Swordfish.Library.Collections;
using Swordfish.Library.Constraints;
using Swordfish.Library.Types;
using Swordfish.Library.Threading;
using Swordfish.UI.Elements;

namespace Swordfish.UI;

public interface IUIContext
{
    LockedList<IElement> Elements { get; }

    IMenuBarElement? MenuBar { get; }

    DataBinding<IConstraint> ScaleConstraint { get; }

    DataBinding<float> FontScale { get; }

    DataBinding<float> FontDisplaySize { get; }

    ThreadContext ThreadContext { get; }

    void Initialize();
}
