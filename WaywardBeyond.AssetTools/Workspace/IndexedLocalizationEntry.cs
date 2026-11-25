using WaywardBeyond.AssetTools.Documents;

namespace WaywardBeyond.AssetTools.Workspace;

/// <summary>
///     Single localization key-value entry indexed from a <c>lang/</c> TOML translation file.
/// </summary>
public readonly record struct IndexedLocalizationEntry(
    string Locale,
    string Key,
    string Value,
    string RootId,
    string RootName,
    string FilePath,
    bool IsWritable,
    TomlFieldSpan FieldSpan);
