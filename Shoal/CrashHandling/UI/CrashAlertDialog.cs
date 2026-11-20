using System.Diagnostics;

namespace Shoal.CrashHandling.UI;

/// <summary>
///     Platform dispatcher for the native crash alert dialog.
/// </summary>
internal static class CrashAlertDialog
{
    public const int MAX_CONTACT_LENGTH = 30;
    public const int MAX_DESCRIPTION_LENGTH = 500;

    /// <summary>
    ///     Displays the native crash alert dialog, falling back to stderr if no graphical session is available.
    /// </summary>
    public static void Show(CrashReportContext report, CrashHandlerOptions options)
    {
        var shown = false;

        try
        {
            if (OperatingSystem.IsWindows())
            {
                shown = Win32CrashAlertDialog.TryShow(report, options);
            }
            else if (OperatingSystem.IsLinux())
            {
                shown = GtkCrashAlertDialog.TryShow(report, options);
            }
        }
        catch
        {
            shown = false;
        }

        if (!shown)
        {
            WriteFallbackNotice(report);
        }
    }

    /// <summary>
    ///     Opens the crash output directory in the platform file manager.
    /// </summary>
    public static void OpenCrashFolder(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return;
        }

        try
        {
            if (OperatingSystem.IsWindows())
            {
                var startInfo = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
                startInfo.ArgumentList.Add(directory);
                Process.Start(startInfo);
            }
            else if (OperatingSystem.IsLinux())
            {
                var startInfo = new ProcessStartInfo("xdg-open") { UseShellExecute = false };
                startInfo.ArgumentList.Add(directory);
                Process.Start(startInfo);
            }
            else if (OperatingSystem.IsMacOS())
            {
                var startInfo = new ProcessStartInfo("open") { UseShellExecute = false };
                startInfo.ArgumentList.Add(directory);
                Process.Start(startInfo);
            }
        }
        catch
        {
            //  Opening the folder is best-effort.
        }
    }

    /// <summary>
    ///     Sanitizes optional user contact input to match the feedback limits.
    /// </summary>
    public static string? SanitizeContact(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        string cleaned = raw.Replace("\t", string.Empty, StringComparison.Ordinal).Trim();
        if (cleaned.Length > MAX_CONTACT_LENGTH)
        {
            cleaned = cleaned[..MAX_CONTACT_LENGTH];
        }

        return cleaned.Length > 0 ? cleaned : null;
    }

    /// <summary>
    ///     Sanitizes optional user description input to match the feedback limits.
    /// </summary>
    public static string? SanitizeDescription(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        string cleaned = raw.Trim();
        if (cleaned.Length > MAX_DESCRIPTION_LENGTH)
        {
            cleaned = cleaned[..MAX_DESCRIPTION_LENGTH];
        }

        return cleaned.Length > 0 ? cleaned : null;
    }

    private static void WriteFallbackNotice(CrashReportContext report)
    {
        Console.Error.WriteLine($"[{report.AppName}] Crashed: {report.FaultSummary}");
        Console.Error.WriteLine($"[{report.AppName}] Crash report: {report.ReportPath}");
        if (!string.IsNullOrWhiteSpace(report.DumpPath))
        {
            Console.Error.WriteLine($"[{report.AppName}] Crash dump: {report.DumpPath}");
        }
    }
}
