namespace WaywardBeyond.AssetTools.Workspace;

/// <summary>
///     Explicit root directories searched by <see cref="InstallLocator"/> for deterministic discovery and testing.
/// </summary>
public readonly record struct InstallSearchRoots(
    IReadOnlyList<string> SteamRoots,
    IReadOnlyList<string> DirectInstallCandidates,
    bool ProbeWindowsRegistry = true);
