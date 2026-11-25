namespace WaywardBeyond.AssetTools.Search;

/// <summary>
///     Workspace root and shadowing metadata for an indexed search entry.
/// </summary>
public readonly record struct SearchProvenance(
    string RootName,
    bool IsWritable,
    bool IsShadowed,
    string? ShadowedByRoot = null,
    string? FilePath = null);
