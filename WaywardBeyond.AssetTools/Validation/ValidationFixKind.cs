namespace WaywardBeyond.AssetTools.Validation;

/// <summary>
///     Identifies the automatic quick-fix action associated with a validation finding.
/// </summary>
public enum ValidationFixKind
{
    /// <summary>
    ///     No automatic quick fix is available.
    /// </summary>
    None = 0,

    /// <summary>
    ///     Replaces the targeted TOML span with <see cref="ValidationFinding.SuggestedFixValue"/>.
    /// </summary>
    ReplaceValue = 1,

    /// <summary>
    ///     Strips a leading UTF-8 byte order mark from the file.
    /// </summary>
    StripUtf8Bom = 2,

    /// <summary>
    ///     Normalizes CRLF line endings to LF across the file.
    /// </summary>
    NormalizeLineEndings = 3,

    /// <summary>
    ///     Removes a duplicate element from a TOML array.
    /// </summary>
    RemoveArrayElement = 4,

    /// <summary>
    ///     Inserts a default <c>[Skills.Levels]</c> entry (<c>1 = 100</c>) for an empty skill curve.
    /// </summary>
    InsertDefaultSkillLevels = 5,

    /// <summary>
    ///     Removes a conflicting TOML key-value field line.
    /// </summary>
    RemoveField = 6,

    /// <summary>
    ///     Creates a missing English localization key in the owning writable root.
    /// </summary>
    CreateLocalizationKey = 7
}
