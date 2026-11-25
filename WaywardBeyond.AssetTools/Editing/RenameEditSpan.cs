using WaywardBeyond.AssetTools.Documents;

namespace WaywardBeyond.AssetTools.Editing;

/// <summary>
///     Single TOML span edit planned as part of a cross-file rename preview.
/// </summary>
public readonly record struct RenameEditSpan(
    string FilePath,
    string FieldName,
    int Line,
    TomlTextSpan Span,
    string OldRawText,
    string NewRawText);
