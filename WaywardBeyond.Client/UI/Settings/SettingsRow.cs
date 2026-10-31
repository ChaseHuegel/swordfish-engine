using Swordfish.Library.Globalization;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>One row in a settings category.</summary>
internal abstract class SettingsRow
{
    /// <summary>Gets the row as markup needs it.</summary>
    public abstract SettingsRowState Snapshot(ILocalization localization);
}
