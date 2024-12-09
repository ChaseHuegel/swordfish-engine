using Swordfish.Library.IO;

namespace WaywardBeyond.Client.Core.Characters;

internal sealed class PlayerController
{
    public PlayerController(in IShortcutService shortcutService)
    {
        var forwardShortcut = new Shortcut
        {
            Name = "",
            Category = "",
            Modifiers = ShortcutModifiers.None,
            Key = Key.W,
            IsEnabled = IsForwardEnabled,
            Action = OnForwardPressed,
            Released = OnForwardReleased,
        };
        shortcutService.RegisterShortcut(forwardShortcut);
    }

    private bool IsForwardEnabled()
    {
        return true;
    }

    private void OnForwardPressed()
    {
    }
    
    private void OnForwardReleased()
    {
    }
}