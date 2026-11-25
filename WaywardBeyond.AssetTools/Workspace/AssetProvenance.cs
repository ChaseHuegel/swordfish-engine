namespace WaywardBeyond.AssetTools.Workspace;

/// <summary>
///     Source file, owning root, table index, and shadowed providers for an indexed asset.
/// </summary>
public readonly record struct AssetProvenance(
    string ProviderRootId,
    string ProviderRootName,
    string FilePath,
    string RelativePath,
    bool IsWritable,
    int EntryIndex,
    int TableIndex,
    IReadOnlyList<AssetShadowEntry> ShadowedProviders)
{
    /// <summary>
    ///     Gets a value indicating whether this asset overrides one or more earlier definitions.
    /// </summary>
    public bool IsOverride => ShadowedProviders.Count > 0;
}
