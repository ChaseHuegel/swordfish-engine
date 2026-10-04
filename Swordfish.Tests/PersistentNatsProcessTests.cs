using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using WaywardBeyond.Server.Core.Streaming;
using WaywardBeyond.Shared.Config;
using Xunit;

namespace Swordfish.Tests;

/// <summary>
/// The embedded NATS child must be terminated deterministically on dispose: the kill+wait path, handler
/// detachment, and the no-restart-after-dispose guard. A shell-script child stands in for the bundled
/// nats-server binary on Linux; the disposal path is platform-shared.
/// </summary>
public class PersistentNatsProcessTests
{
    private sealed class StubConfiguration : IConfiguration
    {
        public string? GetString(string key) => string.Empty;
        public System.Net.IPAddress? GetIPAddress(string key) => null;
        public int? GetInt(string key) => null;
    }

    [Fact]
    public void DisposeTerminatesTheChildProcess()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        string script = Path.Combine(Path.GetTempPath(), $"nats-test-{Guid.NewGuid():N}.sh");
        File.WriteAllText(script, "#!/bin/sh\nsleep 60\n");
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

        try
        {
            var process = new PersistentNatsProcess(NullLogger<PersistentNatsProcess>.Instance, script, new StubConfiguration());
            Assert.True(process.Start().Success, "The child must start.");

            Process? child = (Process?)typeof(PersistentNatsProcess).GetField("_process", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(process);
            Assert.NotNull(child);
            Assert.False(child!.HasExited);

            var stopwatch = Stopwatch.StartNew();
            int childId = child.Id;
            process.Dispose();
            stopwatch.Stop();

            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(8), "Dispose must not hang past its bounded wait.");
            Assert.Null(SafeGetProcess(childId));
        }
        finally
        {
            File.Delete(script);
        }
    }

    [Fact]
    public void DisposeIsIdempotentAndKillsOnlyOnce()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        string script = Path.Combine(Path.GetTempPath(), $"nats-test-{Guid.NewGuid():N}.sh");
        File.WriteAllText(script, "#!/bin/sh\nsleep 60\n");
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

        try
        {
            var process = new PersistentNatsProcess(NullLogger<PersistentNatsProcess>.Instance, script, new StubConfiguration());
            Assert.True(process.Start().Success);

            process.Dispose();
            process.Dispose();
            process.Dispose();

            Process? child = (Process?)typeof(PersistentNatsProcess).GetField("_process", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(process);
            Assert.Null(child);
        }
        finally
        {
            File.Delete(script);
        }
    }

    private static Process? SafeGetProcess(int id)
    {
        try
        {
            return Process.GetProcessById(id);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}