namespace WaywardBeyond.AssetTools.Documents;

/// <summary>
///     Span and parsed metadata for one key-value field in a TOML document.
/// </summary>
public readonly record struct TomlFieldSpan(
    string TablePath,
    int TableIndex,
    string Key,
    IReadOnlyList<string> KeySegments,
    string RawKey,
    TomlTextSpan KeySpan,
    TomlTextSpan EqualsSpan,
    TomlTextSpan ValueSpan,
    TomlTextSpan LineSpan,
    string RawValue,
    TomlValueKind ValueKind,
    object? TypedValue,
    IReadOnlyList<TomlArrayElementSpan> ArrayElements);
