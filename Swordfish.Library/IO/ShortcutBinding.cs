#nullable enable
using System;
using System.Collections.Generic;
using System.Text;

namespace Swordfish.Library.IO;

/// <summary>Represents a bound keyboard key or mouse button with optional modifiers.</summary>
public readonly struct ShortcutBinding : IEquatable<ShortcutBinding>
{
    /// <summary>An empty, unbound shortcut binding.</summary>
    public static readonly ShortcutBinding Unbound = default;

    private readonly bool _hasKey;
    private readonly Key _key;
    private readonly MouseButton _mouseButton;
    private readonly ShortcutModifiers _modifiers;

    /// <summary>Initializes a keyboard binding with optional modifiers.</summary>
    public ShortcutBinding(Key key, ShortcutModifiers modifiers = ShortcutModifiers.None)
    {
        if (key == Key.None || (int)key == 0)
        {
            _hasKey = false;
            _key = Key.None;
            _mouseButton = MouseButton.Unknown;
            _modifiers = ShortcutModifiers.None;
            return;
        }

        _hasKey = true;
        _key = key;
        _mouseButton = MouseButton.Unknown;
        _modifiers = StripSelfModifier(key, modifiers);
    }

    /// <summary>Initializes a mouse button binding with optional modifiers.</summary>
    public ShortcutBinding(MouseButton mouseButton, ShortcutModifiers modifiers = ShortcutModifiers.None)
    {
        if (mouseButton == MouseButton.Unknown)
        {
            _hasKey = false;
            _key = Key.None;
            _mouseButton = MouseButton.Unknown;
            _modifiers = ShortcutModifiers.None;
            return;
        }

        _hasKey = false;
        _key = Key.None;
        _mouseButton = mouseButton;
        _modifiers = modifiers;
    }

    /// <summary>Gets the bound keyboard key, or <see cref="Key.None"/> when unbound or mouse-bound.</summary>
    public Key Key => _hasKey ? _key : Key.None;

    /// <summary>Gets the bound mouse button, or <see cref="MouseButton.Unknown"/> when unbound or key-bound.</summary>
    public MouseButton MouseButton => _mouseButton;

    /// <summary>Gets the required modifier keys.</summary>
    public ShortcutModifiers Modifiers => IsBound ? _modifiers : ShortcutModifiers.None;

    /// <summary>Gets whether this binding targets a keyboard key.</summary>
    public bool IsKey => _hasKey;

    /// <summary>Gets whether this binding targets a mouse button.</summary>
    public bool IsMouseButton => _mouseButton != MouseButton.Unknown;

    /// <summary>Gets whether this binding targets any key or mouse button.</summary>
    public bool IsBound => _hasKey || _mouseButton != MouseButton.Unknown;

    /// <summary>Serializes this binding into a canonical TOML configuration string.</summary>
    public string ToConfigString()
    {
        if (!IsBound)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        AppendConfigModifiers(builder, _modifiers);
        if (_hasKey)
        {
            builder.Append(_key.ToString());
        }
        else
        {
            builder.Append("Mouse.");
            builder.Append(_mouseButton.ToString());
        }

        return builder.ToString();
    }

    /// <summary>Parses a serialized binding string into a <see cref="ShortcutBinding"/>.</summary>
    public static bool TryParse(string? value, out ShortcutBinding binding)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            binding = Unbound;
            return false;
        }

        string[] parts = value.Split('+');
        var modifiers = ShortcutModifiers.None;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            string token = parts[i].Trim();
            if (!TryParseModifier(token, out ShortcutModifiers flag) || (modifiers & flag) != 0)
            {
                binding = Unbound;
                return false;
            }

            modifiers |= flag;
        }

        string trigger = parts[parts.Length - 1].Trim();
        if (trigger.Length == 0)
        {
            binding = Unbound;
            return false;
        }

        if (trigger.StartsWith("Mouse.", StringComparison.OrdinalIgnoreCase))
        {
            string buttonToken = trigger.Substring("Mouse.".Length);
            if (!int.TryParse(buttonToken, out _)
                && Enum.TryParse(buttonToken, ignoreCase: true, out MouseButton mouseButton)
                && Enum.IsDefined(typeof(MouseButton), mouseButton)
                && mouseButton != MouseButton.Unknown)
            {
                binding = new ShortcutBinding(mouseButton, modifiers);
                return true;
            }

            binding = Unbound;
            return false;
        }

        if (!int.TryParse(trigger, out _)
            && Enum.TryParse(trigger, ignoreCase: true, out Key key)
            && Enum.IsDefined(typeof(Key), key)
            && key != Key.None
            && (int)key != 0)
        {
            binding = new ShortcutBinding(key, modifiers);
            return true;
        }

        binding = Unbound;
        return false;
    }

    /// <summary>Formats this binding for UI display.</summary>
    public string ToDisplayString()
    {
        if (!IsBound)
        {
            return string.Empty;
        }

        var parts = new List<string>(4);
        if ((_modifiers & ShortcutModifiers.Control) != 0)
        {
            parts.Add("Ctrl");
        }

        if ((_modifiers & ShortcutModifiers.Shift) != 0)
        {
            parts.Add("Shift");
        }

        if ((_modifiers & ShortcutModifiers.Alt) != 0)
        {
            parts.Add("Alt");
        }

        parts.Add(_hasKey ? _key.ToDisplayString() : FormatMouseButton(_mouseButton));
        return string.Join("+", parts);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return ToDisplayString();
    }

    /// <inheritdoc/>
    public bool Equals(ShortcutBinding other)
    {
        return IsBound == other.IsBound
            && IsKey == other.IsKey
            && Key == other.Key
            && MouseButton == other.MouseButton
            && Modifiers == other.Modifiers;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is ShortcutBinding other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(IsBound, IsKey, Key, MouseButton, Modifiers);
    }

    /// <summary>Returns whether two bindings represent the same trigger and modifiers.</summary>
    public static bool operator ==(ShortcutBinding left, ShortcutBinding right) => left.Equals(right);

    /// <summary>Returns whether two bindings differ in trigger or modifiers.</summary>
    public static bool operator !=(ShortcutBinding left, ShortcutBinding right) => !left.Equals(right);

    private static ShortcutModifiers StripSelfModifier(Key key, ShortcutModifiers modifiers)
    {
        return key switch
        {
            Key.Control => modifiers & ~ShortcutModifiers.Control,
            Key.Shift => modifiers & ~ShortcutModifiers.Shift,
            Key.Alt => modifiers & ~ShortcutModifiers.Alt,
            _ => modifiers,
        };
    }

    private static bool TryParseModifier(string token, out ShortcutModifiers modifier)
    {
        if (string.Equals(token, "Control", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "Ctrl", StringComparison.OrdinalIgnoreCase))
        {
            modifier = ShortcutModifiers.Control;
            return true;
        }

        if (string.Equals(token, "Shift", StringComparison.OrdinalIgnoreCase))
        {
            modifier = ShortcutModifiers.Shift;
            return true;
        }

        if (string.Equals(token, "Alt", StringComparison.OrdinalIgnoreCase))
        {
            modifier = ShortcutModifiers.Alt;
            return true;
        }

        modifier = ShortcutModifiers.None;
        return false;
    }

    private static void AppendConfigModifiers(StringBuilder builder, ShortcutModifiers modifiers)
    {
        if ((modifiers & ShortcutModifiers.Control) != 0)
        {
            builder.Append("Control+");
        }

        if ((modifiers & ShortcutModifiers.Shift) != 0)
        {
            builder.Append("Shift+");
        }

        if ((modifiers & ShortcutModifiers.Alt) != 0)
        {
            builder.Append("Alt+");
        }
    }

    private static string FormatMouseButton(MouseButton mouseButton)
    {
        return mouseButton switch
        {
            MouseButton.Left => "LMB",
            MouseButton.Right => "RMB",
            MouseButton.Middle => "MMB",
            _ => string.Empty,
        };
    }
}
