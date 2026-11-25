namespace WaywardBeyond.AssetTools.Settings;

/// <summary>
///     Resolves per-user or portable directories for settings, drafts, and crash recovery manifests.
/// </summary>
public sealed class ToolDataPaths
{
    /// <summary>
    ///     Marker file name placed beside the executable to enable portable storage mode.
    /// </summary>
    public const string PORTABLE_MARKER_FILE = "portable";

    private const string SETTINGS_FILE_NAME = "settings.toml";
    private const string DRAFTS_DIRECTORY_NAME = "drafts";
    private const string RECOVERY_DIRECTORY_NAME = "recovery";
    private const string VENDOR_DIRECTORY_NAME = "WaywardBeyond";
    private const string APP_DIRECTORY_NAME = "AssetStudio";

    private ToolDataPaths(string dataDirectory, bool isPortable)
    {
        DataDirectory = Path.GetFullPath(dataDirectory);
        IsPortable = isPortable;
    }

    /// <summary>
    ///     Gets a value indicating whether portable mode is active.
    /// </summary>
    public bool IsPortable { get; }

    /// <summary>
    ///     Gets the root directory holding settings, drafts, and recovery data.
    /// </summary>
    public string DataDirectory { get; }

    /// <summary>
    ///     Gets the full path to the tool settings TOML file.
    /// </summary>
    public string SettingsFilePath => Path.Combine(DataDirectory, SETTINGS_FILE_NAME);

    /// <summary>
    ///     Gets the full path to the drafts directory.
    /// </summary>
    public string DraftsDirectory => Path.Combine(DataDirectory, DRAFTS_DIRECTORY_NAME);

    /// <summary>
    ///     Gets the full path to the multi-file save recovery directory.
    /// </summary>
    public string RecoveryDirectory => Path.Combine(DataDirectory, RECOVERY_DIRECTORY_NAME);

    /// <summary>
    ///     Resolves tool data paths, selecting portable mode when a <c>portable</c> file exists next to the executable.
    /// </summary>
    public static ToolDataPaths Resolve(string? executableDirectory = null, string? userDataRoot = null)
    {
        string resolvedExeDir = string.IsNullOrWhiteSpace(executableDirectory)
            ? AppContext.BaseDirectory
            : Path.GetFullPath(executableDirectory);

        string markerPath = Path.Combine(resolvedExeDir, PORTABLE_MARKER_FILE);
        if (File.Exists(markerPath))
        {
            return new ToolDataPaths(resolvedExeDir, isPortable: true);
        }

        string resolvedUserRoot = ResolveUserDataRoot(userDataRoot);
        string appDataDir = Path.Combine(resolvedUserRoot, VENDOR_DIRECTORY_NAME, APP_DIRECTORY_NAME);
        return new ToolDataPaths(appDataDir, isPortable: false);
    }

    private static string ResolveUserDataRoot(string? explicitRoot)
    {
        if (!string.IsNullOrWhiteSpace(explicitRoot))
        {
            return Path.GetFullPath(explicitRoot);
        }

        if (OperatingSystem.IsLinux())
        {
            string? xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (!string.IsNullOrWhiteSpace(xdgDataHome))
            {
                return Path.GetFullPath(xdgDataHome);
            }
        }

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            return localAppData;
        }

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".local", "share");
    }
}
