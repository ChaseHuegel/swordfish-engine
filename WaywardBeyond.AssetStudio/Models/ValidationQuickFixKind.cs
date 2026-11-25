namespace WaywardBeyond.AssetStudio.Models;

/// <summary>
///     Identifies the automated quick-fix workflow available for a validation finding.
/// </summary>
public enum ValidationQuickFixKind
{
    /// <summary>
    ///     No automated quick fix is available for this finding.
    /// </summary>
    None,

    /// <summary>
    ///     Creates a missing localization translation key in an English localization table.
    /// </summary>
    CreateLocalizationKey,

    /// <summary>
    ///     Rewrites a case-only path or tag mismatch to match exact disk casing.
    /// </summary>
    FixPathCase,

    /// <summary>
    ///     Replaces an invalid reference, grammar, or enum value with the suggested fix.
    /// </summary>
    ReplaceReference,

    /// <summary>
    ///     Replaces an invalid scalar or normalized field value with the deterministic suggested value.
    /// </summary>
    ReplaceValue,

    /// <summary>
    ///     Applies house TOML formatting to resolve formatting or style findings.
    /// </summary>
    FormatFile,

    /// <summary>
    ///     Strips a leading UTF-8 byte-order mark from the TOML file.
    /// </summary>
    StripUtf8Bom,

    /// <summary>
    ///     Normalizes CRLF line endings to LF line endings in the TOML file.
    /// </summary>
    NormalizeLineEndings,

    /// <summary>
    ///     Removes an invalid or duplicate entry from an array field.
    /// </summary>
    RemoveArrayElement,

    /// <summary>
    ///     Inserts a starter <c>[Skills.Levels]</c> progression table with <c>1 = 100</c>.
    /// </summary>
    InsertDefaultSkillLevels,

    /// <summary>
    ///     Removes a disallowed or redundant field from a TOML entry table.
    /// </summary>
    RemoveField,
}
