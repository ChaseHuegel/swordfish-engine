namespace WaywardBeyond.AssetTools.Documents;

/// <summary>
///     Span and parsed metadata for one element inside a TOML array value.
/// </summary>
public readonly record struct TomlArrayElementSpan(
    int Index,
    TomlTextSpan ValueSpan,
    TomlTextSpan? CommaSpan,
    string RawValue,
    TomlValueKind ValueKind,
    object? TypedValue);
