namespace WaywardBeyond.AssetStudio.Models;

/// <summary>
///     Describes which workspace root supplies an asset and whether it overrides a lower root.
/// </summary>
public enum AssetProvenance
{
    /// <summary>
    ///     Authored directly inside the active writable pack root.
    /// </summary>
    Pack,

    /// <summary>
    ///     Overrides an existing asset ID or virtual path from a lower root.
    /// </summary>
    Override,

    /// <summary>
    ///     Supplied read-only from the reference game install root.
    /// </summary>
    Base,

    /// <summary>
    ///     Owned by a writable module inside a repository checkout.
    /// </summary>
    Repo,
}
