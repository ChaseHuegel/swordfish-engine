using Swordfish.Library.Collections;
using Swordfish.Library.IO;

namespace Swordfish.Input;

public class ShortcutService : IShortcutService
{
    private readonly IInputService _inputService;
    private readonly LockedList<Shortcut> _shortcuts;

    public ShortcutService(IInputService inputService)
    {
        _inputService = inputService;
        _shortcuts = [];

        _inputService.KeyPressed += OnKeyPressed;
    }

    public bool RegisterShortcut(Shortcut shortcut)
    {
        if (_shortcuts.Any(x => x.Name == shortcut.Name))
        {
            return false;
        }

        _shortcuts.Add(shortcut);
        return true;
    }

    private void OnKeyPressed(object? sender, KeyEventArgs e)
    {
        var modifiers = ShortcutModifiers.None;

        if (_inputService.IsKeyHeld(Key.Control))
        {
            modifiers |= ShortcutModifiers.Control;
        }

        if (_inputService.IsKeyHeld(Key.Shift))
        {
            modifiers |= ShortcutModifiers.Shift;
        }

        if (_inputService.IsKeyHeld(Key.Alt))
        {
            modifiers |= ShortcutModifiers.Alt;
        }

        foreach (Shortcut shortcut in _shortcuts)
        {
            if (shortcut.IsEnabled != null && !shortcut.IsEnabled.Invoke())
            {
                continue;
            }

            if (e.Key == shortcut.Key && (shortcut.Modifiers == ShortcutModifiers.None || modifiers == shortcut.Modifiers))
            {
                shortcut.Action?.Invoke();
            }
        }
    }
}
