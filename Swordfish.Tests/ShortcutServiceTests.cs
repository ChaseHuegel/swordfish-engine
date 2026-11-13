using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Input;
using Swordfish.Library.IO;
using Xunit;

namespace Swordfish.Tests;

/// <summary>Pins the exact modifier match that a shortcut press requires.</summary>
public class ShortcutServiceTests
{
    [Fact]
    public void UnmodifiedShortcutIgnoresHeldModifiers()
    {
        var input = new FakeInputService();
        var shortcuts = new ShortcutService(input, NullLogger.Instance);
        var invocations = 0;

        shortcuts.RegisterShortcut(new Shortcut(
            "Take screenshot",
            "General",
            ShortcutModifiers.None,
            Key.F12,
            Shortcut.DefaultEnabled,
            () => invocations++
        ));

        input.HeldKeys.Add(Key.Shift);
        input.KeyPressed?.Invoke(input, new KeyEventArgs(Key.F12));
        Assert.Equal(0, invocations);

        input.HeldKeys.Remove(Key.Shift);
        input.KeyPressed?.Invoke(input, new KeyEventArgs(Key.F12));
        Assert.Equal(1, invocations);
    }

    [Fact]
    public void ModifiedShortcutRequiresTheExactModifiers()
    {
        var input = new FakeInputService();
        var shortcuts = new ShortcutService(input, NullLogger.Instance);
        var invocations = 0;

        shortcuts.RegisterShortcut(new Shortcut(
            "Reload",
            "General",
            ShortcutModifiers.Control,
            Key.F12,
            Shortcut.DefaultEnabled,
            () => invocations++
        ));

        input.HeldKeys.Add(Key.Shift);
        input.KeyPressed?.Invoke(input, new KeyEventArgs(Key.F12));
        Assert.Equal(0, invocations);

        input.HeldKeys.Add(Key.Control);
        input.KeyPressed?.Invoke(input, new KeyEventArgs(Key.F12));
        Assert.Equal(0, invocations);

        input.HeldKeys.Remove(Key.Shift);
        input.KeyPressed?.Invoke(input, new KeyEventArgs(Key.F12));
        Assert.Equal(1, invocations);
    }

    [Fact]
    public void ModifierKeyShortcutFiresForItsOwnPress()
    {
        var input = new FakeInputService();
        var shortcuts = new ShortcutService(input, NullLogger.Instance);
        var invocations = 0;

        shortcuts.RegisterShortcut(new Shortcut(
            "Snap placement",
            "Interaction",
            ShortcutModifiers.None,
            Key.Shift,
            Shortcut.DefaultEnabled,
            () => invocations++
        ));

        input.HeldKeys.Add(Key.Shift);
        input.KeyPressed?.Invoke(input, new KeyEventArgs(Key.Shift));
        Assert.Equal(1, invocations);
    }
}
