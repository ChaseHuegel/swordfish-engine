namespace WaywardBeyond.AssetTools.Workspace;

/// <summary>
///     One ordered module or content-pack root participating in a workspace.
/// </summary>
public readonly record struct WorkspaceRoot(
    string Id,
    string Name,
    string RootDirectory,
    string AssetsDirectory,
    bool IsWritable,
    bool IsEnabled,
    int LoadOrderIndex,
    ModuleManifestData? Manifest);
