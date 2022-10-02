using ImGuiNET;

namespace Swordfish.UI.Elements;

public class MenuItemElement : ContentElement, INameProperty
{
    public string? Name { get; set; }

    public MenuItemElement(string? name)
    {
        Name = name;
    }

    protected override void OnRender()
    {
        if (Content.Count == 0)
        {
            ImGui.MenuItem(Name);
        }
        else if (ImGui.BeginMenu(Name, true))
        {
            base.OnRender();
            ImGui.EndMenu();
        }
    }
}
