using WaywardBeyond.AssetTools.Workspace;

namespace WaywardBeyond.AssetTools.Editing;

/// <summary>
///     Captured raw TOML or binary asset payload with source provenance for clipboard copy and paste operations.
/// </summary>
public readonly record struct AssetClipboardPayload(
    AssetKind AssetKind,
    string SourceAssetId,
    string RootId,
    string SourceFilePath,
    int SourceTableIndex,
    string? RawTomlText = null,
    byte[]? RawBytes = null);
