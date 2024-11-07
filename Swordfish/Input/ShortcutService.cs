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
        var modifiers = ShortcutModifiers.NONE;

        if (_inputService.IsKeyHeld(Key.CONTROL))
        {
            modifiers |= ShortcutModifiers.CONTROL;
        }

        if (_inputService.IsKeyHeld(Key.SHIFT))
        {
            modifiers |= ShortcutModifiers.SHIFT;
        }

        if (_inputService.IsKeyHeld(Key.ALT))
        {
            modifiers |= ShortcutModifiers.ALT;
        }

        foreach (Shortcut shortcut in _shortcuts)
        {
            if (shortcut.IsEnabled != null && !shortcut.IsEnabled.Invoke())
            {
                continue;
            }

            if (e.Key == shortcut.Key && (shortcut.Modifiers == ShortcutModifiers.NONE || modifiers == shortcut.Modifiers))
            {
                shortcut.Action?.Invoke();
            }
        }
    }
}
