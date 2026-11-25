using System;
using Swordfish.Library.IO;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>A settings entry backed by one rebindable shortcut binding.</summary>
internal sealed class KeybindingEntry(
    string id,
    string labelKey,
    string shortcutId,
    string? sharedBindingGroup,
    KeybindingScope scope,
    Func<ShortcutBinding> get,
    Action<ShortcutBinding> set,
    ShortcutBinding defaultBinding
) : SettingsEntry<ShortcutBinding>(id, labelKey, get, set, () => defaultBinding, isEnabled: null)
{
    public string ShortcutId { get; } = shortcutId;

    public string? SharedBindingGroup { get; } = sharedBindingGroup;

    public KeybindingScope Scope { get; } = scope;

    public ShortcutBinding DefaultBinding { get; } = defaultBinding;

    public bool IsCapturing { get; set; }

    public bool HasConflict { get; set; }

    public bool IsUnbound => !Value.IsBound;

    public bool CanReset => Value != DefaultBinding;

    /// <inheritdoc/>
    public override SettingsRowState Snapshot()
    {
        return SettingsRowState.Keybind(
            Id,
            LabelKey,
            Value.ToDisplayString(),
            IsCapturing,
            HasConflict,
            IsUnbound,
            CanReset
        );
    }
}
