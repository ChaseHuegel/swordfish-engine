namespace WaywardBeyond.AssetTools.Storage;

/// <summary>
///     Structured conflict details returned when a file changed externally since editing began.
/// </summary>
public readonly record struct FileSaveConflict(
    string FilePath,
    string? ExpectedBaseSha256,
    string? ActualDiskSha256,
    FileSaveConflictReason Reason);
