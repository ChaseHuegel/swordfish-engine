namespace Shoal.CrashHandling;

/// <summary>
///     User-initiated crash report submission payload containing the crash context and optional user fields.
/// </summary>
public readonly record struct CrashReportSubmission(
    CrashReportContext Report,
    string? Description,
    string? Contact
);
