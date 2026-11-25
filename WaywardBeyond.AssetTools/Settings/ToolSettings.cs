namespace WaywardBeyond.AssetTools.Settings;

/// <summary>
///     Per-user Asset Studio configuration persisted as TOML.
/// </summary>
public sealed class ToolSettings
{
    /// <summary>
    ///     Minimum supported UI scale factor.
    /// </summary>
    public const double MIN_UI_SCALE = 0.75;

    /// <summary>
    ///     Maximum supported UI scale factor.
    /// </summary>
    public const double MAX_UI_SCALE = 1.75;

    /// <summary>
    ///     Default UI scale factor.
    /// </summary>
    public const double DEFAULT_UI_SCALE = 1.0;

    private double _uiScale = DEFAULT_UI_SCALE;

    /// <summary>
    ///     Gets the ordered list of recently opened workspace directories.
    /// </summary>
    public List<string> RecentWorkspaces { get; } = [];

    /// <summary>
    ///     Gets the persisted workbench panel layout preferences.
    /// </summary>
    public PanelLayoutSettings PanelLayout { get; } = new();

    /// <summary>
    ///     Gets the per-workspace default namespace overrides keyed by normalized workspace root path.
    /// </summary>
    public Dictionary<string, string> WorkspaceDefaultNamespaces { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Gets or sets an optional BCP-47 culture override, or empty to follow the OS culture.
    /// </summary>
    public string CultureOverride { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the preferred visual theme.
    /// </summary>
    public ToolTheme Theme { get; set; } = ToolTheme.System;

    /// <summary>
    ///     Gets or sets the interface scale factor, clamped to the supported range.
    /// </summary>
    public double UiScale
    {
        get => _uiScale;
        set => _uiScale = ClampUiScale(value);
    }

    /// <summary>
    ///     Gets or sets the preferred OpenGL context backend.
    /// </summary>
    public GlBackendPreference GlPreference { get; set; } = GlBackendPreference.Auto;

    /// <summary>
    ///     Gets or sets the diagnostic log verbosity level.
    /// </summary>
    public ToolLogLevel LogLevel { get; set; } = ToolLogLevel.Information;

    /// <summary>
    ///     Clamps <paramref name="scale"/> into <see cref="MIN_UI_SCALE"/>..<see cref="MAX_UI_SCALE"/>.
    /// </summary>
    public static double ClampUiScale(double scale)
    {
        if (double.IsNaN(scale) || double.IsInfinity(scale))
        {
            return DEFAULT_UI_SCALE;
        }

        return Math.Clamp(scale, MIN_UI_SCALE, MAX_UI_SCALE);
    }

    /// <summary>
    ///     Gets the persisted default namespace for <paramref name="workspaceRoot"/>, or null when unset.
    /// </summary>
    public string? GetDefaultNamespace(string workspaceRoot)
    {
        string? normalized = NormalizeWorkspaceRoot(workspaceRoot);
        if (normalized == null)
        {
            return null;
        }

        return WorkspaceDefaultNamespaces.TryGetValue(normalized, out string? value) &&
               !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }

    /// <summary>
    ///     Sets or clears the persisted default namespace for <paramref name="workspaceRoot"/>.
    /// </summary>
    public void SetDefaultNamespace(string workspaceRoot, string? defaultNamespace)
    {
        string? normalized = NormalizeWorkspaceRoot(workspaceRoot);
        if (normalized == null)
        {
            return;
        }

        string cleaned = (defaultNamespace ?? string.Empty).Trim().TrimEnd(':').Trim();
        if (cleaned.Length == 0)
        {
            WorkspaceDefaultNamespaces.Remove(normalized);
            return;
        }

        WorkspaceDefaultNamespaces[normalized] = cleaned;
    }

    /// <summary>
    ///     Adds or promotes <paramref name="workspacePath"/> to the top of recent workspaces.
    /// </summary>
    public void AddRecentWorkspace(string workspacePath, int maxEntries = 10)
    {
        if (string.IsNullOrWhiteSpace(workspacePath) || maxEntries <= 0)
        {
            return;
        }

        string normalized = Path.GetFullPath(workspacePath);
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        for (int i = RecentWorkspaces.Count - 1; i >= 0; i--)
        {
            if (string.Equals(RecentWorkspaces[i], normalized, comparison))
            {
                RecentWorkspaces.RemoveAt(i);
            }
        }

        RecentWorkspaces.Insert(0, normalized);
        while (RecentWorkspaces.Count > maxEntries)
        {
            RecentWorkspaces.RemoveAt(RecentWorkspaces.Count - 1);
        }
    }

    /// <summary>
    ///     Removes all matching occurrences of <paramref name="workspacePath"/> from recent workspaces.
    /// </summary>
    public bool RemoveRecentWorkspace(string workspacePath)
    {
        if (string.IsNullOrWhiteSpace(workspacePath) || workspacePath.IndexOf('\0') >= 0)
        {
            return false;
        }

        string trimmed = workspacePath.Trim();
        string? normalized = NormalizeWorkspaceRoot(trimmed);
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        bool removed = false;
        for (int i = RecentWorkspaces.Count - 1; i >= 0; i--)
        {
            string existing = RecentWorkspaces[i];
            string? existingNormalized = NormalizeWorkspaceRoot(existing);
            bool matchesRaw = string.Equals(existing.Trim(), trimmed, comparison);
            bool matchesNormalized = normalized != null
                && existingNormalized != null
                && string.Equals(existingNormalized, normalized, comparison);
            if (matchesRaw || matchesNormalized)
            {
                RecentWorkspaces.RemoveAt(i);
                removed = true;
            }
        }

        return removed;
    }

    private static string? NormalizeWorkspaceRoot(string workspaceRoot)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot) || workspaceRoot.IndexOf('\0') >= 0)
        {
            return null;
        }

        string full = Path.GetFullPath(workspaceRoot.Trim());
        string? pathRoot = Path.GetPathRoot(full);
        if (string.Equals(full, pathRoot, StringComparison.Ordinal))
        {
            return full;
        }

        return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
