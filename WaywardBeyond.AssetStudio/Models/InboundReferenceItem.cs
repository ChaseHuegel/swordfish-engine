namespace WaywardBeyond.AssetStudio.Models;

/// <summary>
///     Identifies another asset or file that references the active document.
/// </summary>
public readonly record struct InboundReferenceItem(
    string SourceAssetId,
    AssetKind SourceKind,
    string FieldPath,
    string SourceFile);
