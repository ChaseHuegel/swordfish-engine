namespace WaywardBeyond.AssetTools.Search;

/// <summary>
///     High-level category of an indexed command palette entry.
/// </summary>
public enum SearchEntryKind
{
    /// <summary>
    ///     Defined game or engine asset (item, brick, paint, skill, body, material, blueprint, localization, tag).
    /// </summary>
    Asset = 0,

    /// <summary>
    ///     File resource such as a texture, mesh, or sound.
    /// </summary>
    Resource = 1,

    /// <summary>
    ///     Executable workbench command or action.
    /// </summary>
    Action = 2
}
