namespace WaywardBeyond.AssetTools.Workspace;

/// <summary>
///     Read-only data projection of a Shoal module <c>manifest.toml</c> file.
/// </summary>
public readonly record struct ModuleManifestData(
    string Id,
    string Name,
    string Description,
    string Author,
    string Website,
    string Source,
    string? RootPathOverride,
    string? ScriptsPath,
    string? AssembliesPath,
    IReadOnlyList<string> Assemblies);
