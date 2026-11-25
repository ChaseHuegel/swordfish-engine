using Swordfish.Library.IO;
using Xunit;

namespace Swordfish.Tests;

/// <summary>Covers <see cref="ShortcutBinding"/> normalization, serialization, parsing, and formatting.</summary>
public class ShortcutBindingTests
{
    [Fact]
    public void UnboundVariantsNormalizeToTheSameValue()
    {
        ShortcutBinding defaulted = default;
        var fromKeyNone = new ShortcutBinding(Key.None, ShortcutModifiers.Control);
        var fromZeroKey = new ShortcutBinding((Key)0, ShortcutModifiers.Alt);
        var fromMouseUnknown = new ShortcutBinding(MouseButton.Unknown, ShortcutModifiers.Shift);

        Assert.Equal(ShortcutBinding.Unbound, defaulted);
        Assert.Equal(ShortcutBinding.Unbound, fromKeyNone);
        Assert.Equal(ShortcutBinding.Unbound, fromZeroKey);
        Assert.Equal(ShortcutBinding.Unbound, fromMouseUnknown);

        Assert.False(defaulted.IsBound);
        Assert.False(defaulted.IsKey);
        Assert.False(defaulted.IsMouseButton);
        Assert.Equal(Key.None, defaulted.Key);
        Assert.Equal(MouseButton.Unknown, defaulted.MouseButton);
        Assert.Equal(ShortcutModifiers.None, defaulted.Modifiers);
        Assert.Equal(string.Empty, defaulted.ToConfigString());
        Assert.Equal(string.Empty, defaulted.ToDisplayString());
    }

    [Fact]
    public void ModifierKeyStripsSelfFromModifiers()
    {
        var shift = new ShortcutBinding(Key.Shift, ShortcutModifiers.Shift);
        var control = new ShortcutBinding(Key.Control, ShortcutModifiers.Control | ShortcutModifiers.Alt);
        var alt = new ShortcutBinding(Key.Alt, ShortcutModifiers.Alt);

        Assert.Equal(ShortcutModifiers.None, shift.Modifiers);
        Assert.Equal(ShortcutModifiers.Alt, control.Modifiers);
        Assert.Equal(ShortcutModifiers.None, alt.Modifiers);
    }

    [Theory]
    [InlineData("W", Key.W, MouseButton.Unknown, ShortcutModifiers.None, "W", "W")]
    [InlineData("Alt+Enter", Key.Enter, MouseButton.Unknown, ShortcutModifiers.Alt, "Alt+Enter", "Alt+Enter")]
    [InlineData("Control+Shift+F5", Key.F5, MouseButton.Unknown, ShortcutModifiers.Control | ShortcutModifiers.Shift, "Control+Shift+F5", "Ctrl+Shift+F5")]
    [InlineData("ctrl+d1", Key.D1, MouseButton.Unknown, ShortcutModifiers.Control, "Control+D1", "Ctrl+1")]
    [InlineData("Mouse.Left", Key.None, MouseButton.Left, ShortcutModifiers.None, "Mouse.Left", "LMB")]
    [InlineData("Shift+Mouse.Right", Key.None, MouseButton.Right, ShortcutModifiers.Shift, "Shift+Mouse.Right", "Shift+RMB")]
    [InlineData("Mouse.Middle", Key.None, MouseButton.Middle, ShortcutModifiers.None, "Mouse.Middle", "MMB")]
    public void ConfigStringRoundTripsAndFormatsDisplay(
        string input,
        Key expectedKey,
        MouseButton expectedMouse,
        ShortcutModifiers expectedModifiers,
        string expectedConfig,
        string expectedDisplay
    )
    {
        bool parsed = ShortcutBinding.TryParse(input, out ShortcutBinding binding);

        Assert.True(parsed);
        Assert.True(binding.IsBound);
        Assert.Equal(expectedKey, binding.Key);
        Assert.Equal(expectedMouse, binding.MouseButton);
        Assert.Equal(expectedModifiers, binding.Modifiers);
        Assert.Equal(expectedConfig, binding.ToConfigString());
        Assert.Equal(expectedDisplay, binding.ToDisplayString());
        Assert.True(ShortcutBinding.TryParse(expectedConfig, out ShortcutBinding reparsed));
        Assert.Equal(binding, reparsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("None")]
    [InlineData("0")]
    [InlineData("65")]
    [InlineData("Mouse.Unknown")]
    [InlineData("Mouse.99")]
    [InlineData("Mouse.Invalid")]
    [InlineData("Ctrl+")]
    [InlineData("+W")]
    [InlineData("Ctrl+Ctrl+W")]
    [InlineData("Meta+W")]
    [InlineData("InvalidKey")]
    public void MalformedConfigStringsAreRejected(string? input)
    {
        bool parsed = ShortcutBinding.TryParse(input, out ShortcutBinding binding);

        Assert.False(parsed);
        Assert.Equal(ShortcutBinding.Unbound, binding);
    }

    [Fact]
    public void SpecialKeysFormatFriendlyDisplayLabels()
    {
        Assert.Equal("Ctrl", Key.Control.ToDisplayString());
        Assert.Equal("Caps Lock", Key.Capslock.ToDisplayString());
        Assert.Equal("Left", Key.LeftArrow.ToDisplayString());
        Assert.Equal("Up", Key.UpArrow.ToDisplayString());
        Assert.Equal("Page Up", Key.PageUp.ToDisplayString());
        Assert.Equal("Num 4", Key.Numpad4.ToDisplayString());
        Assert.Equal("Num +", Key.Add.ToDisplayString());
        Assert.Equal("/", Key.Slash.ToDisplayString());
    }
}
