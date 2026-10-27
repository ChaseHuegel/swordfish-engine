using System;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>A full-width action that resets one settings category.</summary>
internal sealed class SettingsResetRow(string id, Action reset) : SettingsRow
{
    public string Id { get; } = id;

    /// <inheritdoc/>
    public override int Height => SettingsMetrics.RESET_ROW_HEIGHT;

    /// <inheritdoc/>
    public override void Render(SettingsRenderer renderer, int y)
    {
        renderer.DrawResetRow(Id, y, reset);
    }
}
