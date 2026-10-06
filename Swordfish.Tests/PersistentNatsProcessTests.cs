using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
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
/// nats-server binary on Linux; the disposal path is platform-shared. The owner/shared decision and the
/// supervisor's promotion path are covered with a local listener.
/// </summary>
public class PersistentNatsProcessTests
{
    private sealed class StubConfiguration(string natsUrl) : IConfiguration
    {
        public string? GetString(string key) => key == "NATS_URL" ? natsUrl : null;
        public IPAddress? GetIPAddress(string key) => null;
        public int? GetInt(string key) => null;
    }

    [Fact]
    public void DisposeTerminatesTheChildProcess()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        string script = CreateSleepScript();

        try
        {
            var process = new PersistentNatsProcess(NullLogger<PersistentNatsProcess>.Instance, script, new StubConfiguration(""), detectExistingServer: false);
            Assert.True(process.Start().Success, "The child must start.");

            Process? child = GetChild(process);
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

        string script = CreateSleepScript();

        try
        {
            var process = new PersistentNatsProcess(NullLogger<PersistentNatsProcess>.Instance, script, new StubConfiguration(""), detectExistingServer: false);
            Assert.True(process.Start().Success);

            process.Dispose();
            process.Dispose();
            process.Dispose();

            Assert.Null(GetChild(process));
        }
        finally
        {
            File.Delete(script);
        }
    }

    [Fact]
    public void ReachableAddressSelectsSharedMode()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var process = new PersistentNatsProcess(NullLogger<PersistentNatsProcess>.Instance, "unused", new StubConfiguration($"nats://127.0.0.1:{port}"));
        try
        {
            Assert.True(process.Start().Success);
            Assert.Null(GetChild(process));
        }
        finally
        {
            process.Dispose();
        }
    }

    [Fact]
    public void LosingTheSharedServerPromotesToOwner()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        string script = CreateSleepScript();
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var process = new PersistentNatsProcess(NullLogger<PersistentNatsProcess>.Instance, script, new StubConfiguration($"nats://127.0.0.1:{port}"));
        try
        {
            Assert.True(process.Start().Success);
            Assert.Null(GetChild(process));

            //  The shared server goes away; the supervisor must start its own child.
            listener.Stop();

            var deadline = DateTime.UtcNow.AddSeconds(15);
            Process? child = null;
            while (DateTime.UtcNow < deadline && (child = GetChild(process)) == null)
            {
                Thread.Sleep(100);
            }

            Assert.NotNull(child);
            Assert.False(child!.HasExited);
        }
        finally
        {
            listener.Stop();
            process.Dispose();
            File.Delete(script);
        }
    }

    private static string CreateSleepScript()
    {
        string script = Path.Combine(Path.GetTempPath(), $"nats-test-{Guid.NewGuid():N}.sh");
        File.WriteAllText(script, "#!/bin/sh\nexec sleep 60\n");
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        return script;
    }

    private static Process? GetChild(PersistentNatsProcess process)
    {
        return (Process?)typeof(PersistentNatsProcess).GetField("_process", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(process);
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
