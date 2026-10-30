using Swordfish.Library.Globalization;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>One fixed-height row in a settings category.</summary>
internal abstract class SettingsRow
{
    /// <summary>Gets the fixed row height in pixels.</summary>
    public abstract int Height { get; }

    /// <summary>Gets the row as markup needs it.</summary>
    public abstract SettingsRowState Snapshot(ILocalization localization);
}
