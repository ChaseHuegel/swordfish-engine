using ImGuiNET;
using Ninject;

namespace Swordfish.UI.Elements;

public class Canvas : WindowElement
{
    private IUIContext UIContext => uiContext ??= SwordfishEngine.Kernel.Get<IUIContext>();
    private IUIContext? uiContext;

    public Canvas(string name) : base(name)
    {
        UIContext.Elements.Add(this);
    }
}
