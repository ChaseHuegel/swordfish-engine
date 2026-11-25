namespace WaywardBeyond.AssetStudio.Models;

/// <summary>
///     Describes a single diagnostic finding produced by workspace validation.
/// </summary>
public readonly record struct ValidationFinding(
    string Id,
    ValidationSeverity Severity,
    string RootName,
    string RelativeFile,
    int LineNumber,
    string AssetId,
    string? FieldKey,
    string Message,
    bool CanFixCase = false,
    string? SuggestedFix = null,
    string Code = "",
    ValidationQuickFixKind QuickFixKind = ValidationQuickFixKind.None,
    string? QuickFixLabel = null,
    string? TargetKey = null,
    bool IsSafeBulkFix = false,
    string? FixDescription = null,
    string? AbsoluteFilePath = null,
    int BackendFindingIndex = -1)
{
    /// <summary>
    ///     Whether the finding severity should render with danger badge styling.
    /// </summary>
    public bool IsDangerSeverity => Severity == ValidationSeverity.Fatal || Severity == ValidationSeverity.Broken;

    /// <summary>
    ///     Whether the finding severity should render with warning badge styling.
    /// </summary>
    public bool IsWarningSeverity => Severity == ValidationSeverity.CrossPlatform;

    /// <summary>
    ///     Whether this finding targets a specific named form field.
    /// </summary>
    public bool HasFieldKey => !string.IsNullOrWhiteSpace(FieldKey);

    /// <summary>
    ///     Whether this finding exposes an actionable quick-fix action.
    /// </summary>
    public bool HasQuickFix => QuickFixKind != ValidationQuickFixKind.None;
}
