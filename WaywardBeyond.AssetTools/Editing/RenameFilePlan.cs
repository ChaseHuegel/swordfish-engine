namespace WaywardBeyond.AssetTools.Editing;

/// <summary>
///     Planned before/after document text and exact spans for one file in a rename operation.
/// </summary>
public readonly record struct RenameFilePlan(
    string FilePath,
    byte[] OriginalBytes,
    byte[] UpdatedBytes,
    string BaseSha256,
    IReadOnlyList<RenameEditSpan> Edits);
