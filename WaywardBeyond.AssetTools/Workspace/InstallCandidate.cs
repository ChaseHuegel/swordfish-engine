namespace WaywardBeyond.AssetTools.Workspace;

/// <summary>
///     Discovered Wayward Beyond game installation directory and discovery source.
/// </summary>
public readonly record struct InstallCandidate(
    string InstallPath,
    string Source,
    bool HasModulesConfig);
