namespace WaywardBeyond.AssetStudio.Models;

/// <summary>
///     Classifies validation findings by runtime impact.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>
    ///     Content cannot load (for example TOML syntax error or duplicate ID).
    /// </summary>
    Fatal,

    /// <summary>
    ///     Runtime reads the field and fails or falls back unexpectedly.
    /// </summary>
    Broken,

    /// <summary>
    ///     Platform-dependent problem such as path casing or line-ending drift.
    /// </summary>
    CrossPlatform,

    /// <summary>
    ///     Legal but semantically risky authoring action.
    /// </summary>
    Style,
}
