namespace WaywardBeyond.Client.UI.Settings;

/// <summary>One fixed-height row in a settings category.</summary>
internal abstract class SettingsRow
{
    /// <summary>Gets the fixed row height in pixels.</summary>
    public abstract int Height { get; }

    /// <summary>Draws the row at a content offset.</summary>
    public abstract void Render(SettingsRenderer renderer, int y);
}
