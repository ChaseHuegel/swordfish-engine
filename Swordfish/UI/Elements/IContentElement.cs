using Swordfish.Library.Collections;
// ReSharper disable UnusedMemberInSuper.Global

namespace Swordfish.UI.Elements;

public interface IContentElement : IElement
{
    ContentSeparator ContentSeparator { get; set; }

    LockedObservableCollection<IElement> Content { get; }

    bool AutoScroll { get; set; }
}
