using System.Collections.ObjectModel;
using Swordfish.Library.Collections;

namespace Swordfish.UI.Elements;

public interface IContentElement : IElement
{
    ContentSeparator ContentSeparator { get; set; }

    LockedObservableCollection<IElement> Content { get; }

    bool AutoScroll { get; set; }
}
