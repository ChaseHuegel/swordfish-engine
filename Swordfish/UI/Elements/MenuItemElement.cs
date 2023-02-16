using ImGuiNET;
using Swordfish.Library.IO;

namespace Swordfish.UI.Elements;

public class MenuItemElement : ContentElement, INameProperty
{
    private static IShortcutService ShortcutService => s_ShortcutService ??= SwordfishEngine.Kernel.Get<IShortcutService>();
    private static IShortcutService? s_ShortcutService;

    public string? Name { get; set; }

    public Shortcut Shortcut { get; private set; }

    public MenuItemElement(string? name)
    {
        Name = name;
    }

    public MenuItemElement(string? name, Shortcut shortcut) : this(name)
    {
        shortcut.IsEnabled = ShortcutIsEnabled;
        ShortcutService.RegisterShortcut(shortcut);
        Shortcut = shortcut;
    }

    protected override void OnRender()
    {
        if (Content.Count == 0)
        {
            if (ImGui.MenuItem(Name, Shortcut.ToString(), false, Enabled))
                Shortcut.Action?.Invoke();
        }
        else if (ImGui.BeginMenu(Name, true))
        {
            base.OnRender();
            ImGui.EndMenu();
        }
    }

    private bool ShortcutIsEnabled()
    {
        return Enabled;
    }
}
