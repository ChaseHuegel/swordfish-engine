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
    public override void Render(SettingsRenderer renderer, int y)
    {
        renderer.DrawToggle(this, y);
    }
}
