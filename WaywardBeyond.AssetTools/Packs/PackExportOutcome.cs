using WaywardBeyond.AssetTools.Validation;

namespace WaywardBeyond.AssetTools.Packs;

/// <summary>
///     Outcome of a staged content pack export operation.
/// </summary>
public readonly record struct PackExportOutcome(
    bool Success,
    string? ArchivePath,
    bool BlockedByValidation,
    bool RequiresWarningConfirmation,
    ValidationReport? ValidationReport,
    string? ErrorMessage);
