using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>A labeled section separator inside a settings category.</summary>
internal sealed class SettingsSection(string labelKey) : SettingsRow
{
    public string LabelKey { get; } = labelKey;

    /// <inheritdoc/>
    public override int Height => UiMetrics.SectionHeight;

    /// <inheritdoc/>
    public override void Render(SettingsRenderer renderer, int y)
    {
        renderer.DrawSection(LabelKey, y);
    }
}
