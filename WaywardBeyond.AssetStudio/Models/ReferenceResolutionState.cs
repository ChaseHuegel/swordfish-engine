namespace WaywardBeyond.AssetStudio.Models;

/// <summary>
///     Indicates whether a reference value resolves cleanly in the active workspace.
/// </summary>
public enum ReferenceResolutionState
{
    /// <summary>
    ///     Target exists and matches exact case and grammar.
    /// </summary>
    Resolved,

    /// <summary>
    ///     Target does not exist in any mounted workspace root.
    /// </summary>
    Missing,

    /// <summary>
    ///     Target exists on disk only under different letter casing.
    /// </summary>
    CaseMismatch,

    /// <summary>
    ///     Reference string violates the required field grammar.
    /// </summary>
    InvalidGrammar,

    /// <summary>
    ///     Field has no active runtime consumer today.
    /// </summary>
    Informational,
}
