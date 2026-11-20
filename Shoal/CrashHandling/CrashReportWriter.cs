using System.Runtime.InteropServices;
using System.Text;
using Shoal.Logging;

namespace Shoal.CrashHandling;

/// <summary>
///     Recovers unflushed ring-buffer log entries, writes consolidated crash reports, and prunes old crash artifacts.
/// </summary>
public static class CrashReportWriter
{
    /// <summary>
    ///     Input data collected by the supervisor after a child process exits abnormally.
    /// </summary>
    public sealed record CrashCaptureInput(
        string AppName,
        string AppVersion,
        DateTime TimestampUtc,
        TimeSpan Uptime,
        int ExitCode,
        string FaultSummary,
        string[] Arguments,
        string CrashesDirectory,
        string LogsDirectory,
        string? RingBufferPath,
        CrashSessionMetadata Metadata,
        string StdErrTail,
        string StdOutTail,
        string? DumpPath,
        string? CrashReportJsonPath,
        CrashHandlerConfig Config
    );

    /// <summary>
    ///     Reads any unflushed bytes from the shared ring buffer and appends them to the session log files.
    /// </summary>
    public static string FlushUnflushedRingBytes(string? ringBufferPath, string? sessionLogPath, string? latestLogPath)
    {
        if (string.IsNullOrWhiteSpace(ringBufferPath))
        {
            return string.Empty;
        }

        using LogRingBuffer? reader = LogRingBuffer.TryOpenReader(ringBufferPath);
        if (reader == null)
        {
            return string.Empty;
        }

        byte[] unflushed = reader.ReadUnflushedBytes();
        if (unflushed.Length == 0)
        {
            return string.Empty;
        }

        AppendBytesSafe(sessionLogPath, unflushed);
        AppendBytesSafe(latestLogPath, unflushed);

        return Encoding.UTF8.GetString(unflushed);
    }

    /// <summary>
    ///     Writes a crash report file, appends a crash footer to session logs, prunes old artifacts, and returns the report context.
    /// </summary>
    public static CrashReportContext WriteCrashReport(CrashCaptureInput input)
    {
        Directory.CreateDirectory(input.CrashesDirectory);

        string? sessionLogPath = ResolveExistingPath(input.Metadata.SessionLogPath);
        string? latestLogPath = ResolveExistingPath(input.Metadata.LatestLogPath)
            ?? ResolveExistingPath(Path.Combine(input.LogsDirectory, "latest.log"));

        string recoveredText = FlushUnflushedRingBytes(input.RingBufferPath, sessionLogPath, latestLogPath);

        string stamp = input.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd_HHmm-ss", CultureInfo.InvariantCulture);
        string reportPath = ResolveUniquePath(input.CrashesDirectory, $"crash_{stamp}", ".log");

        string? existingDumpPath = ResolveNonEmptyFile(input.DumpPath);
        string? existingJsonPath = ResolveNonEmptyFile(input.CrashReportJsonPath);

        string sessionLogContent = ReadTextTailFromFile(sessionLogPath ?? latestLogPath, maxBytes: 512 * 1024);
        if (string.IsNullOrEmpty(sessionLogContent) && !string.IsNullOrEmpty(recoveredText))
        {
            sessionLogContent = recoveredText;
        }

        string reportBody = BuildReportText(input, sessionLogPath, existingDumpPath, existingJsonPath, sessionLogContent);
        File.WriteAllText(reportPath, reportBody, Encoding.UTF8);

        string footer = BuildLogFooter(input.TimestampUtc, input.FaultSummary, reportPath, existingDumpPath, input.StdErrTail);
        byte[] footerBytes = Encoding.UTF8.GetBytes(footer);
        AppendBytesSafe(sessionLogPath, footerBytes);
        AppendBytesSafe(latestLogPath, footerBytes);

        PruneOldFiles(input.CrashesDirectory, "crash_*.log", input.Config.MaxCrashReports, reportPath);
        PruneOldFiles(input.CrashesDirectory, "crash_*.dmp", input.Config.MaxCrashDumps, existingDumpPath);
        PruneOldFiles(input.CrashesDirectory, "crash_*.dmp.crashreport.json", input.Config.MaxCrashDumps, existingJsonPath);

        string alertTailSource = BuildAlertTailSource(input, sessionLogContent);
        string truncatedTail = ExtractLogTail(alertTailSource, input.Config.AlertTailLines, input.Config.AlertTailMaxChars);

        return new CrashReportContext(
            AppName: input.AppName,
            AppVersion: input.AppVersion,
            TimestampUtc: input.TimestampUtc,
            ExitCode: input.ExitCode,
            FaultSummary: input.FaultSummary,
            ReportPath: reportPath,
            CrashDirectory: input.CrashesDirectory,
            SessionLogPath: sessionLogPath,
            LatestLogPath: latestLogPath,
            DumpPath: existingDumpPath,
            CrashReportJsonPath: existingJsonPath,
            LogTail: truncatedTail,
            MaxUploadBytes: input.Config.MaxUploadBytes
        );
    }

    /// <summary>
    ///     Truncates text to at most <paramref name="maxLines"/> trailing lines and <paramref name="maxChars"/> trailing characters.
    /// </summary>
    public static string ExtractLogTail(string text, int maxLines, int maxChars)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "(no log output captured)";
        }

        string normalized = text
            .Replace("\0", string.Empty, StringComparison.Ordinal)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .TrimEnd('\n');
        string[] lines = normalized.Split('\n');

        var truncated = false;
        int startIndex = 0;
        if (lines.Length > maxLines)
        {
            startIndex = lines.Length - maxLines;
            truncated = true;
        }

        string result = string.Join(Environment.NewLine, lines, startIndex, lines.Length - startIndex);
        if (result.Length > maxChars)
        {
            result = result[^maxChars..];
            int firstNewline = result.IndexOf('\n');
            if (firstNewline >= 0 && firstNewline + 1 < result.Length)
            {
                result = result[(firstNewline + 1)..];
            }

            truncated = true;
        }

        return truncated ? "... (truncated) ..." + Environment.NewLine + result : result;
    }

    private static string BuildReportText(
        CrashCaptureInput input,
        string? sessionLogPath,
        string? dumpPath,
        string? crashReportJsonPath,
        string sessionLogContent
    )
    {
        var sb = new StringBuilder(4096);
        sb.AppendLine("================================================================================");
        sb.AppendLine($"{input.AppName} Crash Report");
        sb.AppendLine("================================================================================");
        sb.AppendLine($"Timestamp (UTC): {input.TimestampUtc:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"App Version:     {input.AppVersion}");
        sb.AppendLine($"Uptime:          {(int)input.Uptime.TotalHours:D2}:{input.Uptime.Minutes:D2}:{input.Uptime.Seconds:D2}.{input.Uptime.Milliseconds:D3}");
        sb.AppendLine($"Fault:           {input.FaultSummary}");
        sb.AppendLine($"Exit Code:       {input.ExitCode}");
        sb.AppendLine();
        sb.AppendLine("--- System ---");
        sb.AppendLine($"OS:              {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
        sb.AppendLine($"Runtime:         {RuntimeInformation.FrameworkDescription} ({RuntimeInformation.ProcessArchitecture})");
        sb.AppendLine($"CPU Cores:       {Environment.ProcessorCount}");
        sb.AppendLine($"CLI Args:        {(input.Arguments.Length > 0 ? string.Join(' ', input.Arguments) : "(none)")}");

        if (!string.IsNullOrWhiteSpace(input.Metadata.EngineState))
        {
            sb.AppendLine($"Engine State:    {input.Metadata.EngineState}");
        }

        if (!string.IsNullOrWhiteSpace(input.Metadata.GpuRenderer) || !string.IsNullOrWhiteSpace(input.Metadata.GpuVendor))
        {
            sb.AppendLine($"GPU:             {input.Metadata.GpuVendor} / {input.Metadata.GpuRenderer} ({input.Metadata.GpuVersion})");
        }

        if (input.Metadata.LoadedModules.Count > 0)
        {
            sb.AppendLine($"Modules:         {string.Join(", ", input.Metadata.LoadedModules)}");
        }

        if (!string.IsNullOrWhiteSpace(sessionLogPath))
        {
            sb.AppendLine($"Session Log:     {sessionLogPath}");
        }

        if (!string.IsNullOrWhiteSpace(dumpPath))
        {
            sb.AppendLine($"Crash Dump:      {dumpPath}");
        }

        if (!string.IsNullOrWhiteSpace(crashReportJsonPath))
        {
            sb.AppendLine($"Runtime Report:  {crashReportJsonPath}");
        }

        if (!string.IsNullOrWhiteSpace(input.Metadata.UnhandledException))
        {
            sb.AppendLine();
            sb.AppendLine("--- Unhandled Exception ---");
            if (!string.IsNullOrWhiteSpace(input.Metadata.FaultingThread))
            {
                sb.AppendLine($"Thread: {input.Metadata.FaultingThread}");
            }

            sb.AppendLine(input.Metadata.UnhandledException.TrimEnd());
        }

        if (!string.IsNullOrWhiteSpace(input.StdErrTail))
        {
            sb.AppendLine();
            sb.AppendLine("--- Standard Error (tail) ---");
            sb.AppendLine(input.StdErrTail.TrimEnd());
        }
        else if (!string.IsNullOrWhiteSpace(input.StdOutTail))
        {
            sb.AppendLine();
            sb.AppendLine("--- Standard Output (tail) ---");
            sb.AppendLine(input.StdOutTail.TrimEnd());
        }

        sb.AppendLine();
        sb.AppendLine("--- Session Log ---");
        sb.AppendLine(string.IsNullOrWhiteSpace(sessionLogContent) ? "(no session log recorded)" : sessionLogContent.TrimEnd());

        return sb.ToString();
    }

    private static string BuildAlertTailSource(CrashCaptureInput input, string sessionLogContent)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(sessionLogContent))
        {
            sb.AppendLine(sessionLogContent.TrimEnd());
        }

        if (!string.IsNullOrWhiteSpace(input.Metadata.UnhandledException) &&
            (string.IsNullOrWhiteSpace(sessionLogContent) || !sessionLogContent.Contains(input.Metadata.UnhandledException, StringComparison.Ordinal)))
        {
            sb.AppendLine();
            sb.AppendLine("--- Unhandled Exception ---");
            sb.AppendLine(input.Metadata.UnhandledException.TrimEnd());
        }

        if (!string.IsNullOrWhiteSpace(input.StdErrTail))
        {
            sb.AppendLine();
            sb.AppendLine("--- Standard Error ---");
            sb.AppendLine(input.StdErrTail.TrimEnd());
        }

        if (sb.Length == 0)
        {
            sb.AppendLine($"Fault: {input.FaultSummary}");
        }

        return sb.ToString();
    }

    private static string BuildLogFooter(DateTime timestampUtc, string faultSummary, string reportPath, string? dumpPath, string stdErrTail)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine($"{timestampUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff} [CRT] [CrashSupervisor] Process crashed: {faultSummary}");
        sb.AppendLine($"{timestampUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff} [CRT] [CrashSupervisor] Crash report: {reportPath}");
        if (!string.IsNullOrWhiteSpace(dumpPath))
        {
            sb.AppendLine($"{timestampUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff} [CRT] [CrashSupervisor] Crash dump: {dumpPath}");
        }

        if (!string.IsNullOrWhiteSpace(stdErrTail))
        {
            sb.AppendLine("--- Captured stderr ---");
            sb.AppendLine(stdErrTail.TrimEnd());
        }

        return sb.ToString();
    }

    private static string ReadTextTailFromFile(string? path, int maxBytes)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return string.Empty;
        }

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            long length = stream.Length;
            if (length == 0)
            {
                return string.Empty;
            }

            long offset = Math.Max(0, length - maxBytes);
            stream.Seek(offset, SeekOrigin.Begin);

            int toRead = (int)(length - offset);
            var buffer = new byte[toRead];
            int totalRead = 0;
            while (totalRead < toRead)
            {
                int read = stream.Read(buffer, totalRead, toRead - totalRead);
                if (read <= 0)
                {
                    break;
                }

                totalRead += read;
            }

            return Encoding.UTF8.GetString(buffer, 0, totalRead);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void AppendBytesSafe(string? path, byte[] bytes)
    {
        if (string.IsNullOrWhiteSpace(path) || bytes.Length == 0)
        {
            return;
        }

        try
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();
        }
        catch
        {
            //  Best-effort append during crash recovery.
        }
    }

    private static string? ResolveExistingPath(string? path)
    {
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? path : null;
    }

    private static string? ResolveNonEmptyFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            return new FileInfo(path).Length > 0 ? path : null;
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveUniquePath(string directory, string baseName, string extension)
    {
        string candidate = Path.Combine(directory, baseName + extension);
        if (!File.Exists(candidate))
        {
            return candidate;
        }

        for (var i = 1; i < 1000; i++)
        {
            string numbered = Path.Combine(directory, $"{baseName}_{i}{extension}");
            if (!File.Exists(numbered))
            {
                return numbered;
            }
        }

        return Path.Combine(directory, $"{baseName}_{Guid.NewGuid():N}{extension}");
    }

    private static void PruneOldFiles(string directory, string pattern, int maxFiles, string? activePath)
    {
        if (maxFiles <= 0 || !Directory.Exists(directory))
        {
            return;
        }

        try
        {
            string? activeFull = !string.IsNullOrWhiteSpace(activePath) ? Path.GetFullPath(activePath) : null;
            var files = new DirectoryInfo(directory)
                .GetFiles(pattern, SearchOption.TopDirectoryOnly)
                .Where(file => activeFull == null || !Path.GetFullPath(file.FullName).Equals(activeFull, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ToList();

            int keepExisting = Math.Max(0, maxFiles - (activeFull != null ? 1 : 0));
            for (int i = keepExisting; i < files.Count; i++)
            {
                try
                {
                    files[i].Delete();
                }
                catch
                {
                    //  Ignore locked or concurrently deleted files.
                }
            }
        }
        catch
        {
            //  Pruning must never fail crash handling.
        }
    }
}
