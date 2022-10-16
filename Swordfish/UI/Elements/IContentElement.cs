using System.Collections.ObjectModel;
using Swordfish.Library.Types.Collections;

namespace Swordfish.UI.Elements;

public interface IContentElement : IElement
{
    ContentSeparator ContentSeparator { get; set; }

    LockedObservableCollection<IElement> Content { get; }
}
