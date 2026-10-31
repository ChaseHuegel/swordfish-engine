using System;
using Swordfish.Library.Globalization;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>A full-width action that resets one settings category.</summary>
internal sealed class SettingsResetRow(string id, Action reset) : SettingsRow
{
    public string Id { get; } = id;

    /// <inheritdoc/>
    public override SettingsRowState Snapshot(ILocalization localization)
    {
        return SettingsRowState.Reset(Id);
    }

    /// <summary>Restores the category defaults.</summary>
    public void Invoke()
    {
        reset();
    }
}
