namespace WaywardBeyond.AssetTools.Documents;

/// <summary>
///     Span and parsed metadata for a standard or array-of-tables section in a TOML document.
/// </summary>
public readonly record struct TomlTableSpan(
    int Index,
    string HeaderPath,
    IReadOnlyList<string> HeaderSegments,
    bool IsArrayTable,
    int OccurrenceIndex,
    TomlTextSpan HeaderSpan,
    TomlTextSpan HeaderLineSpan,
    TomlTextSpan ContentSpan,
    IReadOnlyList<TomlFieldSpan> Fields,
    bool IsColumnAligned,
    int AlignmentColumn);
