namespace WaywardBeyond.AssetTools.Workspace;

/// <summary>
///     Indexed content asset holding provenance plus authoritative and strict parse results.
/// </summary>
public readonly record struct IndexedAsset(
    AssetKind Kind,
    string Id,
    string DisplayName,
    AssetProvenance Provenance,
    object? AuthoritativeDefinition,
    object? StrictDefinition,
    bool AuthoritativeParseFailed,
    string? AuthoritativeParseError,
    string? StrictParseError);
