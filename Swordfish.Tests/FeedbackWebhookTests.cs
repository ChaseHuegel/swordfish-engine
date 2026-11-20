using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Swordfish.Library.Util;
using WaywardBeyond.Data.Webhooks;
using Xunit;
using Xunit.Abstractions;

namespace Swordfish.Tests;

public class FeedbackWebhookTests(ITestOutputHelper output) : TestBase(output)
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
    public void BuildCrashArchiveIncludesDumpWhenWithinUploadCap()
    {
        string dir = CreateTempDir();
        string reportPath = Path.Combine(dir, "crash.log");
        string sessionPath = Path.Combine(dir, "session.log");
        string dumpPath = Path.Combine(dir, "crash.dmp");
        File.WriteAllText(reportPath, "crash summary");
        File.WriteAllText(sessionPath, "session log");
        File.WriteAllBytes(dumpPath, new byte[256]);

        Result<(byte[] ArchiveBytes, bool IncludedDump)> result = FeedbackWebhook.BuildCrashArchive(
            reportPath,
            sessionPath,
            dumpPath,
            crashReportJsonPath: null,
            maxUploadBytes: 1024 * 1024
        );

        Assert.True(result.Success);
        Assert.True(result.Value.IncludedDump);

        string[] entries = ReadArchiveEntryNames(result.Value.ArchiveBytes);
        Assert.Contains("crash.log", entries);
        Assert.Contains("session.log", entries);
        Assert.Contains("crash.dmp", entries);
    }

    [Fact]
    public void BuildCrashArchiveExcludesDumpWhenExceedingUploadCap()
    {
        string dir = CreateTempDir();
        string reportPath = Path.Combine(dir, "crash.log");
        string sessionPath = Path.Combine(dir, "session.log");
        string dumpPath = Path.Combine(dir, "crash.dmp");
        File.WriteAllText(reportPath, "crash summary");
        File.WriteAllText(sessionPath, "session log");

        var randomDump = new byte[16 * 1024];
        new Random(42).NextBytes(randomDump);
        File.WriteAllBytes(dumpPath, randomDump);

        Result<(byte[] ArchiveBytes, bool IncludedDump)> result = FeedbackWebhook.BuildCrashArchive(
            reportPath,
            sessionPath,
            dumpPath,
            crashReportJsonPath: null,
            maxUploadBytes: 2048
        );

        Assert.True(result.Success);
        Assert.False(result.Value.IncludedDump);

        string[] entries = ReadArchiveEntryNames(result.Value.ArchiveBytes);
        Assert.Contains("crash.log", entries);
        Assert.Contains("session.log", entries);
        Assert.DoesNotContain("crash.dmp", entries);
    }

    private string CreateTempDir()
    {
        string path = Path.Combine(Path.GetTempPath(), $"wb_webhook_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        _tempDirs.Add(path);
        return path;
    }

    private static string[] ReadArchiveEntryNames(byte[] archiveBytes)
    {
        using var stream = new MemoryStream(archiveBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        return archive.Entries.Select(entry => entry.FullName).ToArray();
    }
}
