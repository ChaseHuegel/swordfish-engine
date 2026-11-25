namespace WaywardBeyond.AssetTools.Search;

/// <summary>
///     Workspace provenance filter applied to command palette searches.
/// </summary>
public enum SearchScope
{
    /// <summary>
    ///     Include entries from all workspace roots.
    /// </summary>
    All = 0,

    /// <summary>
    ///     Include only entries from writable roots.
    /// </summary>
    Writable = 1,

    /// <summary>
    ///     Include only entries from read-only reference roots.
    /// </summary>
    Reference = 2,

    /// <summary>
    ///     Include only entries that override or are shadowed by another root.
    /// </summary>
    Overridden = 3
}
