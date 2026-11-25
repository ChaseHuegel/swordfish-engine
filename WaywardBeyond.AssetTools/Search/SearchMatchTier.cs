namespace WaywardBeyond.AssetTools.Search;

/// <summary>
///     Ordered match quality tier used by the command palette ranker.
/// </summary>
public enum SearchMatchTier
{
    /// <summary>
    ///     Case-insensitive exact match against a searchable field.
    /// </summary>
    Exact = 0,

    /// <summary>
    ///     Case-insensitive prefix match against a searchable field.
    /// </summary>
    Prefix = 1,

    /// <summary>
    ///     All query tokens match token prefixes inside a searchable field.
    /// </summary>
    Token = 2,

    /// <summary>
    ///     Fuzzy similarity match computed via FuzzySharp distance.
    /// </summary>
    Fuzzy = 3
}
