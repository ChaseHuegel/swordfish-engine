namespace WaywardBeyond.AssetTools.Search;

/// <summary>
///     Searchable asset, resource, or action item stored in the command palette index.
/// </summary>
public readonly record struct SearchEntry(
    string Id,
    string DisplayName,
    SearchEntryKind Kind,
    SearchProvenance Provenance,
    SearchResolutionState ResolutionState,
    string? AssetKind = null,
    string? LocalizationKey = null,
    string? ResourcePath = null,
    IReadOnlyList<string>? ExtraTokens = null);
