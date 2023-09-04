using Swordfish.Library.Collections;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.IO;

namespace Swordfish.Input;

public class ShortcutService : IShortcutService
{
    private readonly IInputService InputService;
    private readonly LockedList<Shortcut> Shortcuts;

    public ShortcutService(IInputService inputService)
    {
        InputService = inputService;
        Shortcuts = new LockedList<Shortcut>();

        InputService.KeyPressed += OnKeyPressed;

        Debugger.Log("Shortcut service initialize.");
    }

    public bool RegisterShortcut(Shortcut shortcut)
    {
        if (!Shortcuts.Any(x => x.Name == shortcut.Name))
        {
            Shortcuts.Add(shortcut);
            return true;
        }

        return false;
    }

    private void OnKeyPressed(object? sender, KeyEventArgs e)
    {
        ShortcutModifiers modifiers = ShortcutModifiers.NONE;

        if (InputService.IsKeyHeld(Key.CONTROL))
            modifiers |= ShortcutModifiers.CONTROL;

        if (InputService.IsKeyHeld(Key.SHIFT))
            modifiers |= ShortcutModifiers.SHIFT;

        if (InputService.IsKeyHeld(Key.ALT))
            modifiers |= ShortcutModifiers.ALT;

        foreach (Shortcut shortcut in Shortcuts)
        {
            if (shortcut.IsEnabled != null && !shortcut.IsEnabled.Invoke())
                continue;

            if (e.Key == shortcut.Key && (shortcut.Modifiers == ShortcutModifiers.NONE || modifiers == shortcut.Modifiers))
                shortcut.Action?.Invoke();
        }
    }
}
