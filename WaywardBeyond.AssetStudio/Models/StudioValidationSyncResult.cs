namespace WaywardBeyond.AssetStudio.Models;

/// <summary>
///     Bundles fresh workspace validation findings and plain-text report output from the backend.
/// </summary>
public readonly record struct StudioValidationSyncResult(
    bool HasBackendFindings,
    IReadOnlyList<ValidationFinding> Findings,
    string PlainTextReport);
