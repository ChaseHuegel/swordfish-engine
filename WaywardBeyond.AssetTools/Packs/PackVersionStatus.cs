namespace WaywardBeyond.AssetTools.Packs;

/// <summary>
///     Compatibility status of a content pack's <c>ContentVersion</c> against the current tool.
/// </summary>
public enum PackVersionStatus
{
    /// <summary>
    ///     Pack content version matches the tool's supported content version.
    /// </summary>
    Compatible = 0,

    /// <summary>
    ///     Pack content version is newer than the tool; workspace opens read-only.
    /// </summary>
    NewerReadOnly = 1,

    /// <summary>
    ///     Pack content version is older than the tool; full editing is allowed with a warning.
    /// </summary>
    OlderWarning = 2,

    /// <summary>
    ///     Pack has no <c>pack.toml</c> metadata; editing is allowed with an unversioned warning.
    /// </summary>
    UnversionedWarning = 3,

    /// <summary>
    ///     Repository workspace mode does not use pack metadata versioning.
    /// </summary>
    NotApplicableRepo = 4
}
