namespace WaywardBeyond.AssetTools.Workspace;

/// <summary>
///     Directed reference from a source asset field to a target asset, resource, tag, or localization key.
/// </summary>
public readonly record struct InboundReference(
    AssetKind SourceKind,
    string SourceAssetId,
    string SourceFilePath,
    string FieldName,
    string TargetId,
    string TargetGrammar,
    bool IsInformational,
    int Line);
