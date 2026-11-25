namespace WaywardBeyond.AssetStudio.Models;

/// <summary>
///     Describes one file span updated during an explicit cross-file ID rename.
/// </summary>
public readonly record struct RenameAffectedSpan(
    string RelativeFile,
    int LineNumber,
    string FieldPath,
    string BeforeText,
    string AfterText);
