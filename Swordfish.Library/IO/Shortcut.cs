using System;
// ReSharper disable UnusedMember.Global
namespace Swordfish.Library.IO;

public struct Shortcut
{
    public string Name;
    public string Category;
    public string Description;
    public ShortcutModifiers Modifiers;
    public Key Key;
    public MouseButton MouseButton;
    public bool Fixed;
    public bool IgnoreUnboundModifiers;
#nullable enable
    public string? SharedBindingGroup;
#nullable disable
    public Func<bool> IsEnabled;
    public Action Action;
    public Action Released;

    public Shortcut(string name, ShortcutModifiers modifiers, Key key, Func<bool> isEnabled, Action action)
    {
        Name = name;
        Modifiers = modifiers;
        Key = key;
        IsEnabled = isEnabled;
        Action = action;

        Category = null;
        Description = null;
        MouseButton = MouseButton.Unknown;
        Fixed = false;
        IgnoreUnboundModifiers = false;
        SharedBindingGroup = null;
        Released = null;
    }

    public Shortcut(string name, string category, ShortcutModifiers modifiers, Key key, Func<bool> isEnabled, Action action)
    {
        Name = name;
        Category = category;
        Modifiers = modifiers;
        Key = key;
        IsEnabled = isEnabled;
        Action = action;

        Description = null;
        MouseButton = MouseButton.Unknown;
        Fixed = false;
        IgnoreUnboundModifiers = false;
        SharedBindingGroup = null;
        Released = null;
    }

    public Shortcut(string name, string category, string description, ShortcutModifiers modifiers, Key key, Func<bool> isEnabled, Action action)
    {
        Name = name;
        Category = category;
        Description = description;
        Modifiers = modifiers;
        Key = key;
        IsEnabled = isEnabled;
        Action = action;

        MouseButton = MouseButton.Unknown;
        Fixed = false;
        IgnoreUnboundModifiers = false;
        SharedBindingGroup = null;
        Released = null;
    }

    public static bool DefaultEnabled()
    {
        return true;
    }

#nullable enable
    /// <summary>Gets the stable identifier for this shortcut.</summary>
    public string GetId()
    {
        return CreateId(Category, Name);
    }

    /// <summary>Formats a shortcut identifier from category and action name.</summary>
    public static string CreateId(string? category, string name)
    {
        return string.IsNullOrEmpty(category) ? name : $"{category}/{name}";
    }

    /// <summary>Gets the binding declared by this shortcut definition.</summary>
    public ShortcutBinding GetBinding()
    {
        return MouseButton != MouseButton.Unknown
            ? new ShortcutBinding(MouseButton, Modifiers)
            : new ShortcutBinding(Key, Modifiers);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return GetBinding().ToDisplayString();
    }
#nullable disable
}