using WaywardBeyond.AssetTools.Documents;

namespace WaywardBeyond.AssetTools.Validation;

/// <summary>
///     Single validation diagnostic targeting a workspace root, file, line, and optional TOML span.
/// </summary>
public readonly record struct ValidationFinding(
    string Code,
    ValidationSeverity Severity,
    string RootId,
    string FilePath,
    int Line,
    int Column,
    string Field,
    string Message,
    string? SuggestedFixValue = null,
    TomlTextSpan? FieldSpan = null,
    ValidationFixKind FixKind = ValidationFixKind.None,
    bool IsSafeBulkFix = false,
    string? FixDescription = null);
