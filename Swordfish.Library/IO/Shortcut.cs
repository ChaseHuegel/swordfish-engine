using System;
using System.Collections.Generic;
using System.Text;
// ReSharper disable UnusedMember.Global
namespace Swordfish.Library.IO;

public struct Shortcut
{
    public string Name;
    public string Category;
    public string Description;
    public ShortcutModifiers Modifiers;
    public Key Key;
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
    }

    public static bool DefaultEnabled()
    {
        return true;
    }

    public override string ToString()
    {
        if (Key == Key.None)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        var parts = new List<string>();

        if (Modifiers != ShortcutModifiers.None)
        {
            if (Modifiers.HasFlag(ShortcutModifiers.Control))
            {
                parts.Add("Ctrl");
            }

            if (Modifiers.HasFlag(ShortcutModifiers.Shift))
            {
                parts.Add("Shift");
            }

            if (Modifiers.HasFlag(ShortcutModifiers.Alt))
            {
                parts.Add("Alt");
            }

            builder.Append(string.Join(", ", parts));
            builder.Append(" + ");
        }

        builder.Append(Key.ToDisplayString());
        return builder.ToString();

    }
}