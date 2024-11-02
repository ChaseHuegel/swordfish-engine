using ImGuiNET;

namespace Swordfish.UI.Elements;

public class MenuBarElement : AbstractPaneElement
{
    public MenuBarElement(IUIContext uiContext) : base(string.Empty)
    {
        UIContext = uiContext;
        UIContext.Add(this);
    }

    protected override void OnRender()
    {
        ImGui.BeginMainMenuBar();
        base.OnRender();
        ImGui.EndMainMenuBar();
    }
}
