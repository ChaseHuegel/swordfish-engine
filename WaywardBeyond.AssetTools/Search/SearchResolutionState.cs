namespace WaywardBeyond.AssetTools.Search;

/// <summary>
///     Resolution health displayed alongside a command palette search result.
/// </summary>
public enum SearchResolutionState
{
    /// <summary>
    ///     Entry resolves cleanly with no blocking findings.
    /// </summary>
    Resolved = 0,

    /// <summary>
    ///     Entry resolves with non-blocking warnings.
    /// </summary>
    Warning = 1,

    /// <summary>
    ///     Entry has fatal or broken validation findings.
    /// </summary>
    Broken = 2,

    /// <summary>
    ///     Entry is shadowed by a later root in load order.
    /// </summary>
    Shadowed = 3,

    /// <summary>
    ///     Referenced entry could not be resolved to any backing file.
    /// </summary>
    Unresolved = 4
}
