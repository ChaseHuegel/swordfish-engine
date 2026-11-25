namespace WaywardBeyond.AssetTools.Search;

/// <summary>
///     Ranked command palette match containing the matched entry, tier, and numeric score.
/// </summary>
public readonly record struct SearchResult(SearchEntry Entry, SearchMatchTier Tier, int Score);
