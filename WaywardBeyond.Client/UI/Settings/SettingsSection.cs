using WaywardBeyond.Client.UI.Theme;

using Swordfish.Library.Globalization;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>A labeled section separator inside a settings category.</summary>
internal sealed class SettingsSection(string labelKey) : SettingsRow
{
    public string LabelKey { get; } = labelKey;

    /// <inheritdoc/>
    public override int Height => UiMetrics.SECTION_HEIGHT;

    /// <inheritdoc/>
    public override SettingsRowState Snapshot(ILocalization localization)
    {
        return SettingsRowState.Section(localization.GetString(LabelKey) ?? LabelKey);
    }
}
