namespace WaywardBeyond.AssetTools.Search;

/// <summary>
///     Filter and ranking parameters for a command palette query.
/// </summary>
public readonly record struct SearchQuery(
    string Text,
    SearchEntryKind? KindFilter = null,
    string? AssetKindFilter = null,
    SearchScope ScopeFilter = SearchScope.All,
    int MaxResults = 50);
