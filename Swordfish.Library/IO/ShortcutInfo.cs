#nullable enable

namespace Swordfish.Library.IO;

/// <summary>Describes a registered shortcut and its current binding state.</summary>
public readonly struct ShortcutInfo
{
    /// <summary>The stable shortcut identifier.</summary>
    public readonly string Id;

    /// <summary>The display name of the shortcut action.</summary>
    public readonly string Name;

    /// <summary>The category name, or null when uncategorized.</summary>
    public readonly string? Category;

    /// <summary>The optional description of the shortcut.</summary>
    public readonly string? Description;

    /// <summary>The shipped default binding.</summary>
    public readonly ShortcutBinding DefaultBinding;

    /// <summary>The currently active binding.</summary>
    public readonly ShortcutBinding CurrentBinding;

    /// <summary>Whether players may rebind this shortcut.</summary>
    public readonly bool Rebindable;

    /// <summary>Optional group name for actions that intentionally share a binding.</summary>
    public readonly string? SharedBindingGroup;

    /// <summary>Initializes a new shortcut metadata snapshot.</summary>
    public ShortcutInfo(
        string id,
        string name,
        string? category,
        string? description,
        ShortcutBinding defaultBinding,
        ShortcutBinding currentBinding,
        bool rebindable,
        string? sharedBindingGroup
    )
    {
        Id = id;
        Name = name;
        Category = category;
        Description = description;
        DefaultBinding = defaultBinding;
        CurrentBinding = currentBinding;
        Rebindable = rebindable;
        SharedBindingGroup = sharedBindingGroup;
    }
}
