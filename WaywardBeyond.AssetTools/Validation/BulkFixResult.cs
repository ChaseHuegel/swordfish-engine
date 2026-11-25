namespace WaywardBeyond.AssetTools.Validation;

/// <summary>
///     Summary of applying all deterministic, non-destructive safe bulk validation fixes.
/// </summary>
public readonly record struct BulkFixResult(
    int AppliedCount,
    int ModifiedFileCount,
    int SkippedReadOnlyCount,
    IReadOnlyList<string> ModifiedFiles,
    int SkippedCount = 0);
