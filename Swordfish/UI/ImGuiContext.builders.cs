using Swordfish.UI.Elements;

namespace Swordfish.UI;

internal sealed partial class ImGuiContext
{
    public void Add(IElement element)
    {
        if (element is IInternalElement internalElement)
        {
            internalElement.SetUIContext(this);
        }
        
        Elements.Add(element);
    }
}