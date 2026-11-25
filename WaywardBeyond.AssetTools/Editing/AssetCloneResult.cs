using WaywardBeyond.AssetTools.Workspace;

namespace WaywardBeyond.AssetTools.Editing;

/// <summary>
///     Outcome of duplicating or pasting an asset into a writable workspace root.
/// </summary>
public readonly record struct AssetCloneResult(
    AssetKind AssetKind,
    string SourceAssetId,
    string NewAssetId,
    string TargetRootId,
    string TargetFilePath,
    int NewTableIndex,
    int NewEntryIndex,
    bool CreatedNewFile,
    string? UpdatedText = null);
