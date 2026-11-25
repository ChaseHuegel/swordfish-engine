namespace WaywardBeyond.AssetStudio.Models;

/// <summary>
///     Holds version, environment, and runtime diagnostic metadata for the about dialog.
/// </summary>
public readonly record struct AboutDiagnosticsInfo(
    string ToolVersion,
    int ContentVersion,
    string WorkspaceModeLabel,
    string RootsSummary,
    string OsArchitecture,
    string GlVendorRenderer,
    string AvaloniaVersion,
    string LogPath);
