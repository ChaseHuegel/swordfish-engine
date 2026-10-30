using System;
using Swordfish.Library.Globalization;

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
    public override SettingsRowState Snapshot(ILocalization localization)
    {
        return SettingsRowState.Choice(
            Id,
            localization.GetString(LabelKey) ?? LabelKey,
            CanEdit,
            [localization.GetString("ui.settings.value.off") ?? string.Empty,
             localization.GetString("ui.settings.value.on") ?? string.Empty],
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
