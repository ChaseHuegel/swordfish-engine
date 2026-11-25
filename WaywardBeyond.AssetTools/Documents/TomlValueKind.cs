namespace WaywardBeyond.AssetTools.Documents;

/// <summary>
///     TOML syntax value classification for a field or array element.
/// </summary>
public enum TomlValueKind
{
    /// <summary>
    ///     Unrecognized or malformed syntax value.
    /// </summary>
    Unknown = 0,

    /// <summary>
    ///     String literal value.
    /// </summary>
    String = 1,

    /// <summary>
    ///     Signed integer value.
    /// </summary>
    Integer = 2,

    /// <summary>
    ///     Floating-point value.
    /// </summary>
    Float = 3,

    /// <summary>
    ///     Boolean value.
    /// </summary>
    Boolean = 4,

    /// <summary>
    ///     Date, time, or date-time value.
    /// </summary>
    DateTime = 5,

    /// <summary>
    ///     Array value.
    /// </summary>
    Array = 6,

    /// <summary>
    ///     Inline table value.
    /// </summary>
    InlineTable = 7
}
