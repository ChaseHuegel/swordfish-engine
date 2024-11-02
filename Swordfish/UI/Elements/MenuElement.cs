using ImGuiNET;

namespace Swordfish.UI.Elements;

public class MenuElement : AbstractPaneElement
{
    public MenuElement() : base(string.Empty)
    {
        UIContext.Elements.Add(this);
    }

    protected override void OnRender()
    {
        ImGui.BeginMainMenuBar();
        base.OnRender();
        ImGui.EndMainMenuBar();
    }
}
