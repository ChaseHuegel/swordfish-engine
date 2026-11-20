using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Shoal.CrashHandling.UI;
using Shoal.Logging;

namespace Shoal.CrashHandling;

/// <summary>
///     Parent process supervisor that spawns the application child process, captures stdio and crash dumps,
///     recovers unflushed ring-buffer logs on crash, writes crash reports, and presents the crash alert UI.
/// </summary>
public static class CrashSupervisor
{
    public const string FLAG_CRASH_WATCHED = "--crash-watched";
    public const string FLAG_NO_CRASH_HANDLER = "--no-crash-handler";

    private const int STDIO_TAIL_MAX_CHARS = 64 * 1024;

    /// <summary>
    ///     Runs <paramref name="runApp"/> under out-of-process crash supervision, or directly when bypassed.
    /// </summary>
    public static int Run(string[] args, CrashHandlerOptions options, Func<string[], int> runApp)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (runApp == null)
        {
            throw new ArgumentNullException(nameof(runApp));
        }

        bool isWatchedChild = HasFlag(args, FLAG_CRASH_WATCHED);
        string[] cleanArgs = StripInternalFlags(args);

        if (isWatchedChild)
        {
            CrashSession.RefreshFromEnvironment();
            return runApp(cleanArgs);
        }

        AppPaths appPaths = options.AppPaths ?? AppPaths.ForDirectory(new PathInfo(AppContext.BaseDirectory));
        LoggingConfig loggingConfig = LoggingConfig.Load(appPaths);

        if (!loggingConfig.CrashHandler.Enabled || HasFlag(args, FLAG_NO_CRASH_HANDLER) || Debugger.IsAttached)
        {
            return runApp(cleanArgs);
        }

        string? processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return runApp(cleanArgs);
        }

        return RunSupervisorLoop(processPath, cleanArgs, appPaths, loggingConfig, options);
    }

    /// <summary>
    ///     Spawns an explicit child command under crash supervision. Used internally and by cross-process tests.
    /// </summary>
    internal static int RunChildProcess(
        string fileName,
        IReadOnlyList<string> leadingArgs,
        string[] appArgs,
        AppPaths appPaths,
        LoggingConfig loggingConfig,
        CrashHandlerOptions options
    )
    {
        string logsDirectory = appPaths.LogsRoot.Value;
        string crashesDirectory = appPaths.CrashesRoot.Value;
        Directory.CreateDirectory(logsDirectory);
        Directory.CreateDirectory(crashesDirectory);

        string sessionId = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Environment.ProcessId}_{Guid.NewGuid().ToString("N")[..6]}";
        string sessionPrefix = Path.Combine(crashesDirectory, $".session_{sessionId}");
        string ringPath = sessionPrefix + ".ring";
        string metaPath = sessionPrefix + ".meta.json";

        string dumpStamp = DateTime.Now.ToString("yyyy-MM-dd_HHmm-ss", CultureInfo.InvariantCulture);
        string dumpPath = ResolveUniqueDumpPath(crashesDirectory, dumpStamp);
        string crashReportJsonPath = dumpPath + ".crashreport.json";

        bool hasSupervisedConsole = false;
        if (OperatingSystem.IsWindows() && !options.Headless && (HasFlag(appArgs, "-debug") || HasFlag(appArgs, "--debug")))
        {
            hasSupervisedConsole = WindowsConsoleHelper.TryAttachOrAllocate();
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = OperatingSystem.IsWindows() && !hasSupervisedConsole && !options.Headless,
            WorkingDirectory = Environment.CurrentDirectory,
        };

        for (var i = 0; i < leadingArgs.Count; i++)
        {
            startInfo.ArgumentList.Add(leadingArgs[i]);
        }

        startInfo.ArgumentList.Add(FLAG_CRASH_WATCHED);
        for (var i = 0; i < appArgs.Length; i++)
        {
            startInfo.ArgumentList.Add(appArgs[i]);
        }

        startInfo.Environment[CrashSession.ENV_SESSION_PREFIX] = sessionPrefix;
        if (hasSupervisedConsole)
        {
            startInfo.Environment[CrashSession.ENV_SUPERVISED_CONSOLE] = "1";
        }

        if (loggingConfig.CrashHandler.CollectDumps)
        {
            startInfo.Environment["DOTNET_DbgEnableMiniDump"] = "1";
            startInfo.Environment["DOTNET_DbgMiniDumpType"] = ((int)loggingConfig.CrashHandler.DumpType).ToString(CultureInfo.InvariantCulture);
            startInfo.Environment["DOTNET_DbgMiniDumpName"] = dumpPath;
            startInfo.Environment["DOTNET_EnableCrashReport"] = "1";
        }

        var uptime = Stopwatch.StartNew();
        var stdOutTail = new RollingTextBuffer(STDIO_TAIL_MAX_CHARS);
        var stdErrTail = new RollingTextBuffer(STDIO_TAIL_MAX_CHARS);
        int supervisorShutdownRequested = 0;

        using WindowsJobObject? jobObject = WindowsJobObject.TryCreate();
        using var child = new Process { StartInfo = startInfo };

        if (!child.Start())
        {
            return 1;
        }

        jobObject?.TryAssign(child);

        bool sharesWindowsConsole = OperatingSystem.IsWindows() && (options.Headless || hasSupervisedConsole);
        ConsoleCancelEventHandler cancelHandler = (_, e) =>
        {
            e.Cancel = true;
            Volatile.Write(ref supervisorShutdownRequested, 1);
            if (sharesWindowsConsole)
            {
                //  Windows delivers CTRL_C_EVENT to the child in the same console group; allow graceful shutdown before force-kill.
                ScheduleForceKill(child, TimeSpan.FromSeconds(10));
            }
            else
            {
                TryStopChild(child);
            }
        };
        Console.CancelKeyPress += cancelHandler;

        using PosixSignalRegistration? sigTerm = TryRegisterPosixSignal(PosixSignal.SIGTERM, () =>
        {
            Volatile.Write(ref supervisorShutdownRequested, 1);
            TryStopChild(child);
        });

        Task stdOutTask = PumpStreamAsync(child.StandardOutput, Console.Out, stdOutTail);
        Task stdErrTask = PumpStreamAsync(child.StandardError, Console.Error, stdErrTail);

        try
        {
            child.WaitForExit();
            Task.WaitAll([stdOutTask, stdErrTask], TimeSpan.FromSeconds(5));
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }

        uptime.Stop();
        int exitCode = child.ExitCode;

        if (exitCode == 0 || Volatile.Read(ref supervisorShutdownRequested) != 0)
        {
            TryDeleteFile(ringPath);
            TryDeleteFile(metaPath);
            TryDeleteIfEmpty(dumpPath);
            TryDeleteIfEmpty(crashReportJsonPath);
            return exitCode;
        }

        CrashSessionMetadata metadata = CrashSessionMetadata.TryLoadFrom(metaPath);
        string faultSummary = CrashExitDecoder.Describe(exitCode);
        string appVersion = !string.IsNullOrWhiteSpace(options.AppVersion)
            ? options.AppVersion
            : !string.IsNullOrWhiteSpace(metadata.AppVersion)
                ? metadata.AppVersion
                : "unknown";

        var capture = new CrashReportWriter.CrashCaptureInput(
            AppName: options.AppName,
            AppVersion: appVersion,
            TimestampUtc: DateTime.UtcNow,
            Uptime: uptime.Elapsed,
            ExitCode: exitCode,
            FaultSummary: faultSummary,
            Arguments: appArgs,
            CrashesDirectory: crashesDirectory,
            LogsDirectory: logsDirectory,
            RingBufferPath: ringPath,
            Metadata: metadata,
            StdErrTail: stdErrTail.GetText(),
            StdOutTail: stdOutTail.GetText(),
            DumpPath: loggingConfig.CrashHandler.CollectDumps ? dumpPath : null,
            CrashReportJsonPath: loggingConfig.CrashHandler.CollectDumps ? crashReportJsonPath : null,
            Config: loggingConfig.CrashHandler
        );

        CrashReportContext report = CrashReportWriter.WriteCrashReport(capture);

        TryDeleteFile(ringPath);
        TryDeleteFile(metaPath);

        if (options.ShowDialogOverride != null)
        {
            options.ShowDialogOverride(report, options);
        }
        else if (options.Headless)
        {
            WriteHeadlessNotice(report);
        }
        else
        {
            CrashAlertDialog.Show(report, options);
        }

        return exitCode;
    }

    private static int RunSupervisorLoop(
        string processPath,
        string[] cleanArgs,
        AppPaths appPaths,
        LoggingConfig loggingConfig,
        CrashHandlerOptions options
    )
    {
        List<string> leadingArgs = [];
        string[] rawCommandLine = Environment.GetCommandLineArgs();
        if (IsDotNetHost(processPath) && rawCommandLine.Length > 0 && rawCommandLine[0].EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            leadingArgs.Add(rawCommandLine[0]);
        }

        return RunChildProcess(processPath, leadingArgs, cleanArgs, appPaths, loggingConfig, options);
    }

    private static bool IsDotNetHost(string processPath)
    {
        string fileName = Path.GetFileNameWithoutExtension(processPath);
        return fileName.Equals("dotnet", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasFlag(string[] args, string flag)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].Equals(flag, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string[] StripInternalFlags(string[] args)
    {
        var result = new List<string>(args.Length);
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].Equals(FLAG_CRASH_WATCHED, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(args[i]);
        }

        return [.. result];
    }

    private static async Task PumpStreamAsync(StreamReader source, TextWriter mirror, RollingTextBuffer buffer)
    {
        var chunk = new char[4096];
        try
        {
            while (true)
            {
                int read = await source.ReadAsync(chunk, 0, chunk.Length).ConfigureAwait(false);
                if (read <= 0)
                {
                    break;
                }

                buffer.Append(chunk.AsSpan(0, read));
                try
                {
                    await mirror.WriteAsync(chunk, 0, read).ConfigureAwait(false);
                    await mirror.FlushAsync().ConfigureAwait(false);
                }
                catch
                {
                    //  Parent console handle may not exist (e.g. WinExe without console).
                }
            }
        }
        catch
        {
            //  Pipe closed on child exit.
        }
    }

    private static PosixSignalRegistration? TryRegisterPosixSignal(PosixSignal signal, Action handler)
    {
        if (OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            return PosixSignalRegistration.Create(signal, context =>
            {
                context.Cancel = true;
                handler();
            });
        }
        catch
        {
            return null;
        }
    }

    private static void TryStopChild(Process child)
    {
        try
        {
            if (child.HasExited)
            {
                return;
            }

            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                const int SIGTERM = 15;
                if (UnixKill(child.Id, SIGTERM) == 0)
                {
                    ScheduleForceKill(child, TimeSpan.FromSeconds(5));
                    return;
                }
            }
            else if (child.CloseMainWindow())
            {
                ScheduleForceKill(child, TimeSpan.FromSeconds(5));
                return;
            }

            child.Kill(entireProcessTree: true);
        }
        catch
        {
            //  Child already exited.
        }
    }

    private static void ScheduleForceKill(Process child, TimeSpan gracePeriod)
    {
        _ = Task.Run(() =>
        {
            try
            {
                if (!child.WaitForExit((int)gracePeriod.TotalMilliseconds) && !child.HasExited)
                {
                    child.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                //  Child already exited or handle disposed.
            }
        });
    }

    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int UnixKill(int pid, int sig);

    private static string ResolveUniqueDumpPath(string crashesDirectory, string stamp)
    {
        string candidate = Path.Combine(crashesDirectory, $"crash_{stamp}.dmp");
        if (!File.Exists(candidate))
        {
            return candidate;
        }

        for (var i = 1; i < 1000; i++)
        {
            string numbered = Path.Combine(crashesDirectory, $"crash_{stamp}_{i}.dmp");
            if (!File.Exists(numbered))
            {
                return numbered;
            }
        }

        return Path.Combine(crashesDirectory, $"crash_{stamp}_{Guid.NewGuid():N}.dmp");
    }

    private static void WriteHeadlessNotice(CrashReportContext report)
    {
        Console.Error.WriteLine($"[{report.AppName}] Process crashed: {report.FaultSummary}");
        Console.Error.WriteLine($"[{report.AppName}] Crash report written to: {report.ReportPath}");
        if (!string.IsNullOrWhiteSpace(report.DumpPath))
        {
            Console.Error.WriteLine($"[{report.AppName}] Crash dump written to: {report.DumpPath}");
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            //  Ignore cleanup errors.
        }
    }

    private static void TryDeleteIfEmpty(string path)
    {
        try
        {
            if (File.Exists(path) && new FileInfo(path).Length == 0)
            {
                File.Delete(path);
            }
        }
        catch
        {
            //  Ignore cleanup errors.
        }
    }

    private sealed class RollingTextBuffer(int maxChars)
    {
        private readonly object _lock = new();
        private readonly StringBuilder _builder = new(Math.Min(maxChars, 4096));
        private readonly int _maxChars = maxChars;

        public void Append(ReadOnlySpan<char> chars)
        {
            lock (_lock)
            {
                _builder.Append(chars);
                if (_builder.Length > _maxChars)
                {
                    _builder.Remove(0, _builder.Length - _maxChars);
                }
            }
        }

        public string GetText()
        {
            lock (_lock)
            {
                return _builder.ToString();
            }
        }
    }
}
