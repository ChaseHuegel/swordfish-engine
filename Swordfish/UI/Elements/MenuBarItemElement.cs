using DryIoc;
using ImGuiNET;
using Swordfish.Library.IO;

namespace Swordfish.UI.Elements;

public class MenuBarItemElement(in string? name) : ContentElement, INameProperty
{
    private static IShortcutService ShortcutService => _sShortcutService ??= SwordfishEngine.Container.Resolve<IShortcutService>();
    private static IShortcutService? _sShortcutService;

    public string? Name { get; set; } = name;

    public Shortcut Shortcut { get; }

    public MenuBarItemElement(string? name, Shortcut shortcut) : this(name)
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
            {
                Shortcut.Action?.Invoke();
            }
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
