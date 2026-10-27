namespace WaywardBeyond.Client.UI.Settings;

/// <summary>One fixed-height row in a settings category.</summary>
internal abstract class SettingsRow
{
    /// <summary>Gets the fixed row height in pixels.</summary>
    public abstract int Height { get; }

    /// <summary>Draws the row in the current vertical layout.</summary>
    public abstract void Render(SettingsRenderer renderer);
}
