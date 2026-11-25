using WaywardBeyond.AssetTools.Documents;

namespace WaywardBeyond.AssetTools.Workspace;

/// <summary>
///     Single tag list entry indexed from a <c>lang/**/tags/</c> TOML file.
/// </summary>
public readonly record struct IndexedTagEntry(
    string Locale,
    string TagName,
    IReadOnlyList<string> Values,
    string RootId,
    string RootName,
    string FilePath,
    bool IsWritable,
    TomlFieldSpan FieldSpan);
