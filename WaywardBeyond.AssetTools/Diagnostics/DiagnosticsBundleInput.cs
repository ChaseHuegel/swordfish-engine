using WaywardBeyond.AssetTools.Validation;
using WaywardBeyond.AssetTools.Workspace;

namespace WaywardBeyond.AssetTools.Diagnostics;

/// <summary>
///     Metadata and runtime telemetry packaged into a sanitized support diagnostics zip.
/// </summary>
public readonly record struct DiagnosticsBundleInput(
    string ToolVersion,
    uint ContentVersion,
    WorkspaceMode WorkspaceMode,
    IReadOnlyList<string> WorkspaceRoots,
    string GlVendor,
    string GlRenderer,
    string AvaloniaVersion,
    string? LogFilePath,
    ValidationReport? ValidationReport,
    IReadOnlyList<string> RecentErrors);
