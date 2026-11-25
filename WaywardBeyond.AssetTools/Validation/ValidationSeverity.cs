namespace WaywardBeyond.AssetTools.Validation;

/// <summary>
///     Severity levels for workspace validation findings.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>
    ///     Content cannot load (syntax error, missing ID, or fatal schema failure). Blocks export.
    /// </summary>
    Fatal = 0,

    /// <summary>
    ///     Game reads this field and fails (dangling reference, strict enum mismatch, invalid grammar). Blocks export.
    /// </summary>
    Broken = 1,

    /// <summary>
    ///     Platform-dependent behavior (case-only path mismatch, CRLF or BOM drift).
    /// </summary>
    CrossPlatform = 2,

    /// <summary>
    ///     Legal but risky authoring change (tag reorder, brick ID rename, duplicate tag entry).
    /// </summary>
    Style = 3,

    /// <summary>
    ///     Diagnostic on a field that no shipped game code currently reads.
    /// </summary>
    Informational = 4
}
