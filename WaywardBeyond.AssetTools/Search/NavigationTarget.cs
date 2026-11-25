namespace WaywardBeyond.AssetTools.Search;

/// <summary>
///     Identifies a navigable asset, file, or field target stored in navigation history.
/// </summary>
public readonly record struct NavigationTarget(
    string Key,
    string? FilePath = null,
    int? Line = null,
    string? FieldKey = null);
