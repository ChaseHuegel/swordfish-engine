using WaywardBeyond.AssetTools.Settings;

namespace WaywardBeyond.AssetStudio.Models;

/// <summary>
///     Holds per-user studio preferences editable in the settings dialog.
/// </summary>
public readonly record struct StudioSettingsState(
    bool IsDarkTheme,
    string CultureOverride,
    string GlPreference,
    string LogLevel,
    bool IsPortableMode,
    string SettingsPath,
    ToolTheme Theme = ToolTheme.System,
    double UiScale = 1.0,
    string DefaultNamespace = "");
