using Swordfish.Library.Util;

namespace Shoal.CrashHandling;

/// <summary>
///     Configuration passed by a launcher to <see cref="CrashSupervisor.Run"/>.
/// </summary>
public sealed class CrashHandlerOptions
{
    /// <summary>
    ///     Display name of the application shown in crash reports and dialogs.
    /// </summary>
    public required string AppName { get; init; }

    /// <summary>
    ///     Optional application version string recorded in crash reports.
    /// </summary>
    public string? AppVersion { get; init; }

    /// <summary>
    ///     When true, suppresses graphical crash alert dialogs and writes crash details to stderr only.
    /// </summary>
    public bool Headless { get; init; }

    /// <summary>
    ///     Optional report submission handler. When set, the crash dialog exposes optional contact/description fields and a Send Report button.
    /// </summary>
    public Func<CrashReportSubmission, CancellationToken, Task<Result<string>>>? OnSendReportAsync { get; init; }

    /// <summary>
    ///     Resolved application paths for config, logs, and crashes. Defaults to <see cref="AppContext.BaseDirectory"/>.
    /// </summary>
    public AppPaths? AppPaths { get; init; }

    /// <summary>
    ///     Allows tests to intercept crash alert presentation without opening a native window.
    /// </summary>
    internal Action<CrashReportContext, CrashHandlerOptions>? ShowDialogOverride { get; init; }
}
