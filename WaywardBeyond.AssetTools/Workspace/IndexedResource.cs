namespace WaywardBeyond.AssetTools.Workspace;

/// <summary>
///     Indexed texture, mesh, audio, or shader file with virtual asset identifier and provenance.
/// </summary>
public readonly record struct IndexedResource(
    ResourceKind Kind,
    string Id,
    string ProviderRootId,
    string ProviderRootName,
    string FilePath,
    string RelativePath,
    bool IsWritable,
    IReadOnlyList<string> ShadowedRootIds);
