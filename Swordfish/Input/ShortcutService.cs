using Microsoft.Extensions.Logging;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.IO;
using Swordfish.Library.Util;

namespace Swordfish.Input;

public class ShortcutService : IShortcutService
{
    private class RegisteredShortcut(in Shortcut shortcut)
    {
        public readonly Shortcut Shortcut = shortcut;
        public readonly ShortcutState ShortcutState = new();
    }

    private class ShortcutState
    {
        public bool PendingRelease;
    }
    
    private readonly IInputService _inputService;
    private readonly ILogger _logger;
    private readonly Dictionary<string, RegisteredShortcut> _registeredShortcuts;

    public ShortcutService(IInputService inputService, ILogger logger)
    {
        _inputService = inputService;
        _logger = logger;
        _registeredShortcuts = [];

        _inputService.KeyPressed += OnKeyPressed;
        _inputService.KeyReleased += OnKeyReleased;
    }

    public bool RegisterShortcut(Shortcut shortcut)
    {
        lock (_registeredShortcuts)
        {
            return _registeredShortcuts.TryAdd($"{shortcut.Category}/{shortcut.Name}", new RegisteredShortcut(shortcut));
        }
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

        lock (_registeredShortcuts)
        {
            foreach (RegisteredShortcut registration in _registeredShortcuts.Values)
            {
                Shortcut shortcut = registration.Shortcut;
                
                if (e.Key != shortcut.Key || (shortcut.Modifiers != ShortcutModifiers.None && modifiers != shortcut.Modifiers))
                {
                    continue;
                }

                if (shortcut.IsEnabled != null && !shortcut.IsEnabled.Invoke())
                {
                    continue;
                }

                registration.ShortcutState.PendingRelease = true;
                
                Result<Exception> invokeResult = Safe.Invoke(shortcut.Action);
                if (!invokeResult)
                {
                    _logger.LogError(invokeResult.Value, "Caught an exception trying to invoke pressed action {action} for shortcut {shortcut}.", shortcut.Action.Method, shortcut.ToString());
                }
            }
        }
    }
    
    private void OnKeyReleased(object? sender, KeyEventArgs e)
    {
        lock (_registeredShortcuts)
        {
            foreach (RegisteredShortcut registration in _registeredShortcuts.Values)
            {
                Shortcut shortcut = registration.Shortcut;
                if (!registration.ShortcutState.PendingRelease || e.Key != shortcut.Key || shortcut.Released == null)
                {
                    continue;
                }
                
                registration.ShortcutState.PendingRelease = false;
                
                Result<Exception> invokeResult = Safe.Invoke(shortcut.Released);
                if (!invokeResult)
                {
                    _logger.LogError(invokeResult.Value, "Caught an exception trying to invoke release action {action} for shortcut {shortcut}.", shortcut.Action.Method, shortcut.ToString());
                }
            }
        }
    }
}
