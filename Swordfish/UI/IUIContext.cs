using Swordfish.Library.Constraints;
using Swordfish.Library.Types;
using Swordfish.Library.Threading;
using Swordfish.UI.Elements;
// ReSharper disable UnusedMemberInSuper.Global
// ReSharper disable UnusedMember.Global

namespace Swordfish.UI;

public interface IUIContext
{
    DataBinding<IConstraint> ScaleConstraint { get; }

    DataBinding<float> FontScale { get; }

    DataBinding<float> FontDisplaySize { get; }

    ThreadContext ThreadContext { get; }

    void Initialize();

    void Add(IElement element);
    
    CanvasElement NewCanvas(string name);
    MenuBarElement NewMenuBar();
}
