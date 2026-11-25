namespace WaywardBeyond.AssetTools.Workspace;

/// <summary>
///     Earlier workspace provider shadowed by a higher-precedence asset definition.
/// </summary>
public readonly record struct AssetShadowEntry(
    string RootId,
    string RootName,
    string FilePath,
    string RelativePath,
    bool IsWritable,
    int EntryIndex,
    int TableIndex);
