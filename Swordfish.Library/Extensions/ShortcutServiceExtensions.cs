using System;
using Swordfish.Library.IO;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.Extensions;

// ReSharper disable once UnusedType.Global
public static class ShortcutServiceExtensions
{
    public static Shortcut NewShortcut(this IShortcutService shortcutService, string name, ShortcutModifiers modifiers, Key key, Func<bool> isEnabled, Action action)
    {
        var shortcut = new Shortcut(name, modifiers, key, isEnabled, action);
        shortcutService.RegisterShortcut(shortcut);
        return shortcut;
    }
    
    public static Shortcut NewShortcut(this IShortcutService shortcutService, string name, string category, ShortcutModifiers modifiers, Key key, Func<bool> isEnabled, Action action)
    {
        var shortcut = new Shortcut(name, category, modifiers, key, isEnabled, action);
        shortcutService.RegisterShortcut(shortcut);
        return shortcut;
    }
    
    public static Shortcut NewShortcut(this IShortcutService shortcutService, string name, string category, string description, ShortcutModifiers modifiers, Key key, Func<bool> isEnabled, Action action)
    {
        var shortcut = new Shortcut(name, category, description, modifiers, key, isEnabled, action);
        shortcutService.RegisterShortcut(shortcut);
        return shortcut;
    }
}