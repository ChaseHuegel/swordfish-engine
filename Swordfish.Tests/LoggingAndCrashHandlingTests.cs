using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using Shoal.CrashHandling;
using Shoal.CrashHandling.UI;
using Shoal.Logging;
using Swordfish.Library.IO;
using Xunit;
using Xunit.Abstractions;

namespace Swordfish.Tests;

public class LoggingAndCrashHandlingTests(ITestOutputHelper output) : TestBase(output)
{
    private readonly List<string> _tempDirs = [];

    protected override void OnTearDown()
    {
        for (var i = 0; i < _tempDirs.Count; i++)
        {
            try
            {
                if (Directory.Exists(_tempDirs[i]))
                {
                    Directory.Delete(_tempDirs[i], recursive: true);
                }
            }
            catch
            {
                //  Best-effort cleanup.
            }
        }
    }

    [Fact]
    public void LoggingConfigLoadsDefaultsWhenFileMissing()
    {
        string dir = CreateTempDir();

        LoggingConfig config = LoggingConfig.Load(dir);

        Assert.Equal(LogLevel.Information, config.MinLevel);
        Assert.Equal(200, config.FlushIntervalMs);
        Assert.Equal(LogLevel.Error, config.SyncFlushMinLevel);
        Assert.Equal(20, config.MaxLogFiles);
        Assert.True(config.CrashHandler.Enabled);
        Assert.True(config.CrashHandler.CollectDumps);
        Assert.Equal(CrashDumpType.Mini, config.CrashHandler.DumpType);
        Assert.Equal(8 * 1024 * 1024, config.CrashHandler.MaxUploadBytes);
    }

    [Fact]
    public void LoggingConfigParsesTomlAndClampsBounds()
    {
        const string toml = """
            MinLevel = "Debug"
            FlushIntervalMs = 5
            SyncFlushMinLevel = "Warning"
            MaxLogFiles = -3

            [CrashHandler]
            Enabled = false
            MaxCrashReports = 7
            AlertTailLines = 0
            AlertTailMaxChars = 100
            CollectDumps = true
            DumpType = "WithHeap"
            MaxCrashDumps = 3
            MaxUploadBytes = 4194304
            """;

        LoggingConfig config = LoggingConfig.Parse(toml);

        Assert.Equal(LogLevel.Debug, config.MinLevel);
        Assert.Equal(LoggingConfig.MIN_FLUSH_INTERVAL_MS, config.FlushIntervalMs);
        Assert.Equal(LogLevel.Warning, config.SyncFlushMinLevel);
        Assert.Equal(0, config.MaxLogFiles);
        Assert.False(config.CrashHandler.Enabled);
        Assert.Equal(7, config.CrashHandler.MaxCrashReports);
        Assert.Equal(1, config.CrashHandler.AlertTailLines);
        Assert.Equal(512, config.CrashHandler.AlertTailMaxChars);
        Assert.True(config.CrashHandler.CollectDumps);
        Assert.Equal(CrashDumpType.WithHeap, config.CrashHandler.DumpType);
        Assert.Equal(3, config.CrashHandler.MaxCrashDumps);
        Assert.Equal(4194304, config.CrashHandler.MaxUploadBytes);
    }

    [Fact]
    public void LoggingConfigPrefersAppPathsConfigRootOverDefaults()
    {
        string dir = CreateTempDir();
        AppPaths appPaths = AppPaths.ForDirectory(new PathInfo(dir));
        Directory.CreateDirectory(appPaths.ConfigRoot.Value);
        File.WriteAllText(appPaths.ConfigRoot.At("logging.toml").Value, "FlushIntervalMs = 75\nMaxLogFiles = 9\n");

        LoggingConfig config = LoggingConfig.Load(appPaths);

        Assert.Equal(Path.Combine(dir, "crashes"), appPaths.CrashesRoot.Value);
        Assert.Equal(75, config.FlushIntervalMs);
        Assert.Equal(9, config.MaxLogFiles);
    }

    [Fact]
    public void LogRingBufferRecoversUnflushedBytesAcrossWraparound()
    {
        string dir = CreateTempDir();
        string ringPath = Path.Combine(dir, "session.ring");

        using (LogRingBuffer writer = LogRingBuffer.CreateOrOpenWriter(ringPath, capacity: 32))
        {
            long seq1 = writer.Append(Encoding.UTF8.GetBytes("0123456789ABCDEF0123"));
            writer.MarkFlushed(seq1);

            writer.Append(Encoding.UTF8.GetBytes("UNFLUSHED_WRAP_TAIL!"));
        }

        using LogRingBuffer? reader = LogRingBuffer.TryOpenReader(ringPath);
        Assert.NotNull(reader);

        byte[] recovered = reader!.ReadUnflushedBytes();
        Assert.Equal("UNFLUSHED_WRAP_TAIL!", Encoding.UTF8.GetString(recovered));
    }

    [Fact]
    public void LogRingBufferReturnsEmptyWhenFullyFlushed()
    {
        string dir = CreateTempDir();
        string ringPath = Path.Combine(dir, "session.ring");

        using (LogRingBuffer writer = LogRingBuffer.CreateOrOpenWriter(ringPath, capacity: 64))
        {
            long seq = writer.Append(Encoding.UTF8.GetBytes("already flushed"));
            writer.MarkFlushed(seq);
        }

        using LogRingBuffer? reader = LogRingBuffer.TryOpenReader(ringPath);
        Assert.NotNull(reader);
        Assert.Empty(reader!.ReadUnflushedBytes());
    }

    [Fact]
    public void AsyncFileLoggerFlushesImmediatelyOnSyncFlushLevel()
    {
        string dir = CreateTempDir();
        string logsDir = Path.Combine(dir, "logs");

        var config = LoggingConfig.CreateDefault();
        config.FlushIntervalMs = 5000;
        config.SyncFlushMinLevel = LogLevel.Error;

        using var provider = new AsyncFileLoggerProvider(config, logsDir, sessionStamp: "sync_test");
        ILogger logger = provider.CreateLogger("SyncTest");

        logger.LogInformation("buffered info line");
        logger.LogError("critical failure line");

        //  Without disposing, both lines must already be flushed on disk because Error forces a sync drain.
        string latestContent = ReadSharedText(provider.LatestLogPath);
        string sessionContent = ReadSharedText(provider.TimestampedLogPath);

        Assert.Contains("buffered info line", latestContent);
        Assert.Contains("critical failure line", latestContent);
        Assert.Contains("critical failure line", sessionContent);
    }

    [Fact]
    public void AsyncFileLoggerWriteEmergencyDrainsQueuedEntriesInOrder()
    {
        string dir = CreateTempDir();
        string logsDir = Path.Combine(dir, "logs");

        var config = LoggingConfig.CreateDefault();
        config.FlushIntervalMs = 5000;
        config.SyncFlushMinLevel = LogLevel.Critical;

        using var provider = new AsyncFileLoggerProvider(config, logsDir, sessionStamp: "emergency_order");
        ILogger logger = provider.CreateLogger("OrderTest");

        logger.LogInformation("earlier info line");
        provider.WriteEmergency("EMERGENCY CRASH LINE");

        string content = ReadSharedText(provider.LatestLogPath);
        int infoIndex = content.IndexOf("earlier info line", StringComparison.Ordinal);
        int emergencyIndex = content.IndexOf("EMERGENCY CRASH LINE", StringComparison.Ordinal);

        Assert.True(infoIndex >= 0, "Expected earlier buffered info line to be flushed.");
        Assert.True(emergencyIndex > infoIndex, "Expected emergency line to appear after earlier buffered entries.");
    }

    [Fact]
    public void AsyncFileLoggerFlushesBufferedInfoEntriesOnDisposeAndPrunesOldLogs()
    {
        string dir = CreateTempDir();
        string logsDir = Path.Combine(dir, "logs");
        Directory.CreateDirectory(logsDir);

        for (var i = 0; i < 5; i++)
        {
            string oldLog = Path.Combine(logsDir, $"2025-01-0{i + 1}_0000-00.log");
            File.WriteAllText(oldLog, "old");
            File.SetLastWriteTimeUtc(oldLog, new DateTime(2025, 1, i + 1, 0, 0, 0, DateTimeKind.Utc));
        }

        var config = LoggingConfig.CreateDefault();
        config.MaxLogFiles = 3;
        config.FlushIntervalMs = 5000;

        string timestampedPath;
        using (var provider = new AsyncFileLoggerProvider(config, logsDir, sessionStamp: "2026-11-19_1200-00"))
        {
            timestampedPath = provider.TimestampedLogPath;
            ILogger logger = provider.CreateLogger("PruneTest");
            logger.LogInformation("final line before shutdown");
        }

        Assert.Contains("final line before shutdown", File.ReadAllText(timestampedPath));
        Assert.Contains("final line before shutdown", File.ReadAllText(Path.Combine(logsDir, "latest.log")));

        string[] remaining = Directory.GetFiles(logsDir, "*.log");
        //  3 timestamped logs + latest.log = 4 files total.
        Assert.Equal(4, remaining.Length);
    }

    [Fact]
    public void CrashExitDecoderDescribesWindowsNtStatusAndUnixSignals()
    {
        Assert.Contains("STATUS_ACCESS_VIOLATION", CrashExitDecoder.Describe(unchecked((int)0xC0000005), isWindows: true, isUnix: false));
        Assert.Contains("STATUS_STACK_OVERFLOW", CrashExitDecoder.Describe(unchecked((int)0xC00000FD), isWindows: true, isUnix: false));
        Assert.Contains("CLR_UNHANDLED_EXCEPTION", CrashExitDecoder.Describe(unchecked((int)0xE0434352), isWindows: true, isUnix: false));
        Assert.Contains("SIGSEGV", CrashExitDecoder.Describe(139, isWindows: false, isUnix: true));
        Assert.Contains("SIGABRT", CrashExitDecoder.Describe(134, isWindows: false, isUnix: true));
    }

    [Fact]
    public void CrashReportWriterRecoversRingTailWritesReportAppendsFooterAndPrunes()
    {
        string dir = CreateTempDir();
        string logsDir = Path.Combine(dir, "logs");
        string crashesDir = Path.Combine(dir, "crashes");
        Directory.CreateDirectory(logsDir);
        Directory.CreateDirectory(crashesDir);

        string sessionLog = Path.Combine(logsDir, "2026-11-19_1200-00.log");
        string latestLog = Path.Combine(logsDir, "latest.log");
        File.WriteAllText(sessionLog, "line 1 flushed\n");
        File.WriteAllText(latestLog, "line 1 flushed\n");

        string ringPath = Path.Combine(crashesDir, ".session_test.ring");
        using (LogRingBuffer writer = LogRingBuffer.CreateOrOpenWriter(ringPath, capacity: 1024))
        {
            long seq = writer.Append(Encoding.UTF8.GetBytes("line 1 flushed\n"));
            writer.MarkFlushed(seq);
            writer.Append(Encoding.UTF8.GetBytes("line 2 lost before crash\n"));
        }

        string dumpPath = Path.Combine(crashesDir, "crash_2026-11-19_1200-00.dmp");
        File.WriteAllBytes(dumpPath, [1, 2, 3, 4]);

        for (var i = 0; i < 4; i++)
        {
            string oldReport = Path.Combine(crashesDir, $"crash_2025-01-0{i + 1}_0000-00.log");
            File.WriteAllText(oldReport, "old crash");
            File.SetLastWriteTimeUtc(oldReport, new DateTime(2025, 1, i + 1, 0, 0, 0, DateTimeKind.Utc));
        }

        var metadata = new CrashSessionMetadata
        {
            SessionLogPath = sessionLog,
            LatestLogPath = latestLog,
            EngineState = "Running",
            GpuVendor = "TestVendor",
            GpuRenderer = "TestGPU",
            GpuVersion = "4.6",
            LoadedModules = ["Swordfish (swordfish)"],
            FaultingThread = "ECS (ID 7)",
            UnhandledException = "System.InvalidOperationException: Boom",
        };

        var config = new CrashHandlerConfig
        {
            MaxCrashReports = 2,
            MaxCrashDumps = 2,
            AlertTailLines = 20,
            AlertTailMaxChars = 4096,
        };

        CrashReportContext report = CrashReportWriter.WriteCrashReport(new CrashReportWriter.CrashCaptureInput(
            AppName: "TestApp",
            AppVersion: "1.2.3",
            TimestampUtc: new DateTime(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc),
            Uptime: TimeSpan.FromSeconds(12),
            ExitCode: 139,
            FaultSummary: "Signal 11 (SIGSEGV (Segmentation fault), exit code 139)",
            Arguments: ["--debug"],
            CrashesDirectory: crashesDir,
            LogsDirectory: logsDir,
            RingBufferPath: ringPath,
            Metadata: metadata,
            StdErrTail: "Fatal error. Segmentation fault.",
            StdOutTail: string.Empty,
            DumpPath: dumpPath,
            CrashReportJsonPath: null,
            Config: config
        ));

        Assert.True(File.Exists(report.ReportPath));
        Assert.Equal(dumpPath, report.DumpPath);

        string updatedLatest = File.ReadAllText(latestLog);
        Assert.Contains("line 2 lost before crash", updatedLatest);
        Assert.Contains("Crash report:", updatedLatest);

        string reportContent = File.ReadAllText(report.ReportPath);
        Assert.Contains("TestApp Crash Report", reportContent);
        Assert.Contains("line 2 lost before crash", reportContent);
        Assert.Contains("System.InvalidOperationException: Boom", reportContent);
        Assert.Contains("Fatal error. Segmentation fault.", reportContent);
        Assert.Contains("TestVendor / TestGPU", reportContent);

        Assert.Equal(2, Directory.GetFiles(crashesDir, "crash_*.log").Length);
    }

    [Fact]
    public void CrashReportWriterTruncatesLogTailByLinesAndChars()
    {
        var sb = new StringBuilder();
        for (var i = 1; i <= 50; i++)
        {
            sb.AppendLine($"log line {i:D2}");
        }

        string tailByLines = CrashReportWriter.ExtractLogTail(sb.ToString(), maxLines: 5, maxChars: 10000);
        Assert.StartsWith("... (truncated) ...", tailByLines);
        Assert.Contains("log line 50", tailByLines);
        Assert.DoesNotContain("log line 45", tailByLines);

        string tailByChars = CrashReportWriter.ExtractLogTail(sb.ToString(), maxLines: 100, maxChars: 40);
        Assert.StartsWith("... (truncated) ...", tailByChars);
        Assert.Contains("log line 50", tailByChars);
    }

    [Fact]
    public void CrashAlertDialogSanitizesOptionalContactAndDescription()
    {
        Assert.Null(CrashAlertDialog.SanitizeContact("   "));
        Assert.Equal("userhandle", CrashAlertDialog.SanitizeContact("  user\thandle  "));
        Assert.Equal(CrashAlertDialog.MAX_CONTACT_LENGTH, CrashAlertDialog.SanitizeContact(new string('a', 60))!.Length);

        Assert.Null(CrashAlertDialog.SanitizeDescription(" \r\n "));
        Assert.Equal(CrashAlertDialog.MAX_DESCRIPTION_LENGTH, CrashAlertDialog.SanitizeDescription(new string('b', 700))!.Length);
    }

    [Fact]
    public void CrashSupervisorCapturesChildCrashStderrAndWritesReport()
    {
        string dir = CreateTempDir();
        var config = LoggingConfig.CreateDefault();
        config.CrashHandler.CollectDumps = false;

        AppPaths appPaths = AppPaths.ForDirectory(new PathInfo(dir));
        CrashReportContext? capturedReport = null;
        var options = new CrashHandlerOptions
        {
            AppName = "SupervisorTestApp",
            AppVersion = "0.1.0",
            Headless = false,
            AppPaths = appPaths,
            ShowDialogOverride = (report, _) => capturedReport = report,
        };

        string shell;
        string[] leadingArgs;
        if (OperatingSystem.IsWindows())
        {
            shell = "cmd.exe";
            leadingArgs = ["/c", "echo simulated crash stderr 1>&2 & exit /b 42"];
        }
        else
        {
            shell = "/bin/sh";
            leadingArgs = ["-c", "echo 'simulated crash stderr' >&2; exit 139"];
        }

        int exitCode = CrashSupervisor.RunChildProcess(
            shell,
            leadingArgs,
            appArgs: [],
            appPaths: appPaths,
            loggingConfig: config,
            options: options
        );

        Assert.NotEqual(0, exitCode);
        Assert.NotNull(capturedReport);
        Assert.Equal(appPaths.CrashesRoot.Value, capturedReport!.CrashDirectory);
        Assert.True(File.Exists(capturedReport.ReportPath));

        string reportText = File.ReadAllText(capturedReport.ReportPath);
        Assert.Contains("SupervisorTestApp Crash Report", reportText);
        Assert.Contains("simulated crash stderr", reportText);

        //  Temporary session files must be cleaned up after crash handling.
        Assert.Empty(Directory.GetFiles(capturedReport.CrashDirectory, ".session_*"));
    }

    private string CreateTempDir()
    {
        string path = Path.Combine(Path.GetTempPath(), $"swordfish_log_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        _tempDirs.Add(path);
        return path;
    }

    private static string ReadSharedText(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
