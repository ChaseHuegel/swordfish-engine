using WaywardBeyond.AssetTools.Settings;

namespace WaywardBeyond.AssetStudio.Services;

/// <summary>
///     Carries startup paths, log session, GL preference, and CLI launch target from <see cref="Program"/> to <see cref="App"/>.
/// </summary>
internal sealed class AppStartupContext
{
    /// <summary>
    ///     Optional workspace directory passed on the command line to open directly on startup.
    /// </summary>
    public string? InitialWorkspacePath { get; init; }

    /// <summary>
    ///     Resolved per-user or portable data paths for settings, drafts, recovery, and logs.
    /// </summary>
    public required ToolDataPaths DataPaths { get; init; }

    /// <summary>
    ///     Active asynchronous file logging session for the studio process.
    /// </summary>
    public StudioLogSession? LogSession { get; init; }

    /// <summary>
    ///     Configured OpenGL backend preference loaded at startup.
    /// </summary>
    public GlBackendPreference GlPreference { get; init; } = GlBackendPreference.Auto;

    /// <summary>
    ///     Active startup context shared with the Avalonia application instance.
    /// </summary>
    public static AppStartupContext? Current { get; set; }
}
