namespace Shoal.CrashHandling;

/// <summary>
///     Immutable crash artifact context passed to the crash alert dialog and optional report uploader.
/// </summary>
public sealed record CrashReportContext(
    string AppName,
    string AppVersion,
    DateTime TimestampUtc,
    int ExitCode,
    string FaultSummary,
    string ReportPath,
    string CrashDirectory,
    string? SessionLogPath,
    string? LatestLogPath,
    string? DumpPath,
    string? CrashReportJsonPath,
    string LogTail,
    int MaxUploadBytes
);
