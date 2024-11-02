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

    public CanvasElement NewCanvas(string name)
    {
        return new CanvasElement(this, name);
    }

    public MenuBarElement NewMenuBar()
    {
        return new MenuBarElement(this);
    }
}