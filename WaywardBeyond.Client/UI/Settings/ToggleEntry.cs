using System;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>A boolean setting rendered as an on/off control.</summary>
internal sealed class ToggleEntry(
    string id,
    string labelKey,
    Func<bool> get,
    Action<bool> set,
    Func<bool> defaultValue,
    Func<bool>? isEnabled = null
) : SettingsEntry<bool>(id, labelKey, get, set, defaultValue, isEnabled)
{
    /// <inheritdoc/>
    public override SettingsRowState Snapshot()
    {
        return SettingsRowState.Choice(
            Id,
            LabelKey,
            CanEdit,
            ["ui.settings.value.off", "ui.settings.value.on"],
            Value ? 1 : 0
        );
    }

    /// <inheritdoc/>
    public override bool TrySetOptionIndex(int index)
    {
        if (index is < 0 or > 1)
        {
            return false;
        }

        SetValue(index == 1);
        return true;
    }
}
