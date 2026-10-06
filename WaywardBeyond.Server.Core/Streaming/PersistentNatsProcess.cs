using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Server.Core.Interop.Windows;
using WaywardBeyond.Shared.Config;

namespace WaywardBeyond.Server.Core.Streaming;

/// <summary>
/// Ensures a NATS server is reachable for the save store. At start it probes the configured
/// <c>NATS_URL</c>: a reachable address is used as-is (shared mode), otherwise a bundled
/// <c>nats-server</c> child is started. A supervisor keeps the address covered: it promotes a shared
/// process to owner when the existing server goes away, and restarts an owned child that exits.
/// </summary>
public sealed class PersistentNatsProcess : IDisposable
{
    private const string VAR_NATS_EXTRA_ARGS = "NATS_EXTRA_ARGS";
    private const string VAR_NATS_URL = "NATS_URL";
    private const int STOP_WAIT_MS = 5000;
    private const int PROBE_TIMEOUT_MS = 500;
    private const int SUPERVISE_INTERVAL_MS = 2000;

    private readonly ILogger<PersistentNatsProcess> _logger;
    private readonly Lock _lock = new();
    private readonly ProcessStartInfo _startInfo;
    private readonly string _url;
    private readonly string _host;
    private readonly int _port;
    private readonly bool _canHost;
    private readonly bool _detectExistingServer;

    private readonly ManualResetEventSlim _stop = new(false);
    private Thread? _supervisor;

    private Process? _process;
    private Job? _windowsJob;
    private volatile bool _disposed;

    public PersistentNatsProcess(in ILogger<PersistentNatsProcess> logger, in VirtualFileSystem vfs, in IConfiguration configuration)
    {
        _logger = logger;

        PathInfo virtualPath;
        if (OperatingSystem.IsLinux())
        {
            virtualPath = new PathInfo("server/nats/nats-server");
        }
        else if (OperatingSystem.IsWindows())
        {
            virtualPath = new PathInfo("server/nats/nats-server.exe");
        }
        else
        {
            throw new PlatformNotSupportedException();
        }

        if (!vfs.TryGetFile(virtualPath, out PathInfo absolutePath))
        {
            throw new FileNotFoundException("NATS server executable not found.");
        }

        (_host, _port) = ParseNatsUrl(configuration.GetString(VAR_NATS_URL));
        _url = $"{_host}:{_port}";
        _canHost = IsLoopback(_host);
        _detectExistingServer = true;
        _startInfo = CreateStartInfo(absolutePath.Value, configuration, _port);
    }

    internal PersistentNatsProcess(in ILogger<PersistentNatsProcess> logger, string executablePath, in IConfiguration configuration, bool detectExistingServer = true)
    {
        _logger = logger;
        (_host, _port) = ParseNatsUrl(configuration.GetString(VAR_NATS_URL));
        _url = $"{_host}:{_port}";
        _canHost = true;
        _detectExistingServer = detectExistingServer;
        _startInfo = CreateStartInfo(executablePath, configuration, _port);
    }

    private static ProcessStartInfo CreateStartInfo(string executablePath, in IConfiguration configuration, int port)
    {
        string storageDirectory = Path.GetFullPath("saves/").Replace('\\', '/');

        return new ProcessStartInfo(executablePath)
        {
            Arguments = $"-js -sd \"{storageDirectory}\" -p {port} {configuration.GetString(VAR_NATS_EXTRA_ARGS)}",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
    }

    /// <summary>
    /// Selects shared mode when the configured address is already served, otherwise starts the child.
    /// A supervisor then keeps the address covered for the rest of the process lifetime.
    /// </summary>
    public Result Start()
    {
        using Lock.Scope _ = _lock.EnterScope();

        if (_disposed)
        {
            return Result.FromFailure("NATS process is disposed.");
        }

        if (_process != null && !_process.HasExited)
        {
            return Result.FromSuccess();
        }

        if (_detectExistingServer && IsServerReachable(_host, _port, PROBE_TIMEOUT_MS))
        {
            _logger.LogInformation("Using existing NATS server at {url}.", _url);
            EnsureSupervisor();
            return Result.FromSuccess();
        }

        if (_detectExistingServer && !_canHost)
        {
            _logger.LogWarning("No NATS server at {url} and the address is not hostable locally.", _url);
            return Result.FromFailure($"No NATS server at {_url}.");
        }

        Result start = StartOwned();
        if (!start.Success)
        {
            return start;
        }

        EnsureSupervisor();
        return Result.FromSuccess();
    }

    public void Dispose()
    {
        Thread? supervisor;
        using (Lock.Scope _ = _lock.EnterScope())
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            supervisor = _supervisor;
            _supervisor = null;
        }

        _stop.Set();
        supervisor?.Join(STOP_WAIT_MS);

        using (Lock.Scope _ = _lock.EnterScope())
        {
            //  Detach every handler first so teardown can never be followed by a resurrected server:
            //  the Exited handler clears the child so the supervisor restarts it.
            if (_process != null)
            {
                _process.Exited -= OnProcessExited;
                _process.OutputDataReceived -= OnProcessOutput;
                _process.ErrorDataReceived -= OnProcessError;
            }

            try
            {
                if (_process != null && !_process.HasExited)
                {
                    Terminate(_process);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to terminate the NATS server process cleanly.");
            }

            _windowsJob?.Dispose();
            _windowsJob = null;
            _process?.Dispose();
            _process = null;
        }

        _stop.Dispose();
    }

    private void EnsureSupervisor()
    {
        if (_supervisor != null)
        {
            return;
        }

        _supervisor = new Thread(Supervise)
        {
            IsBackground = true,
            Name = "NATS supervisor",
        };
        _supervisor.Start();
    }

    private void Supervise()
    {
        while (!_stop.IsSet)
        {
            bool owned;
            using (Lock.Scope _ = _lock.EnterScope())
            {
                owned = _process != null && !_process.HasExited;
            }

            if (!owned)
            {
                bool covered = _detectExistingServer && IsServerReachable(_host, _port, PROBE_TIMEOUT_MS);
                if (!covered && _canHost)
                {
                    using Lock.Scope _ = _lock.EnterScope();
                    if (_disposed)
                    {
                        break;
                    }

                    if (_process == null || _process.HasExited)
                    {
                        Result start = StartOwned();
                        if (!start.Success)
                        {
                            _logger.LogWarning("NATS server start failed: {message}", start.Message);
                        }
                    }
                }
            }

            _stop.Wait(SUPERVISE_INTERVAL_MS);
        }
    }

    /// <summary>Starts the bundled child. The caller holds <see cref="_lock"/>.</summary>
    private Result StartOwned()
    {
        if (!File.Exists(_startInfo.FileName))
        {
            return Result.FromFailure($"NATS server doesn't exist at \"{_startInfo.FileName}\".");
        }

        if (OperatingSystem.IsLinux())
        {
            try
            {
                UnixFileMode mode = File.GetUnixFileMode(_startInfo.FileName);
                if (!mode.HasFlag(UnixFileMode.UserExecute))
                {
                    File.SetUnixFileMode(_startInfo.FileName, mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set executable permissions on {file}, it may fail to start.", _startInfo.FileName);
            }
        }

        try
        {
            Process? process = Process.Start(_startInfo);
            if (process == null)
            {
                return Result.FromFailure($"NATS server process failed to start at \"{_startInfo.FileName}\".");
            }

            _process = process;
            _process.OutputDataReceived += OnProcessOutput;
            _process.ErrorDataReceived += OnProcessError;
            _process.Exited += OnProcessExited;

            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            _logger.LogInformation("NATS server process started with: {args}", _startInfo.Arguments);

            if (OperatingSystem.IsWindows())
            {
                _windowsJob = new Job();
                _windowsJob.AddProcess(process.Handle);
            }
        }
        catch (Exception ex)
        {
            return new Result(success: false, $"NATS server process failed to start at \"{_startInfo.FileName}\".", ex);
        }

        return Result.FromSuccess();
    }

    /// <summary>
    /// Clears the exited child so the supervisor restarts it. Never restarts during teardown, and never
    /// throws out of the event handler.
    /// </summary>
    private void OnProcessExited(object? sender, EventArgs e)
    {
        using Lock.Scope _ = _lock.EnterScope();

        if (_disposed || _process == null || !ReferenceEquals(_process, sender))
        {
            return;
        }

        _process.Exited -= OnProcessExited;
        _process.OutputDataReceived -= OnProcessOutput;
        _process.ErrorDataReceived -= OnProcessError;
        _process.Dispose();
        _windowsJob?.Dispose();
        _windowsJob = null;
        _process = null;
    }

    private static void Terminate(Process process)
    {
        if (OperatingSystem.IsWindows())
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(STOP_WAIT_MS);
            return;
        }

        //  Ask nats-server to stop so JetStream closes cleanly, then force-kill if it does not.
        try
        {
            using Process? killer = Process.Start(new ProcessStartInfo("kill")
            {
                Arguments = $"-TERM {process.Id}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            killer?.WaitForExit(1000);
        }
        catch
        {
            //  Fall through to the forced kill.
        }

        if (!process.WaitForExit(STOP_WAIT_MS))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(STOP_WAIT_MS);
        }
    }

    private static bool IsServerReachable(string host, int port, int timeoutMs)
    {
        try
        {
            using var client = new TcpClient();
            Task connect = client.ConnectAsync(host, port);
            return connect.Wait(timeoutMs) && client.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsLoopback(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host, out IPAddress? address) && IPAddress.IsLoopback(address);
    }

    private static (string host, int port) ParseNatsUrl(string? url)
    {
        string value = string.IsNullOrWhiteSpace(url) ? "nats://127.0.0.1:4222" : url;
        if (!value.Contains("://", StringComparison.Ordinal))
        {
            value = "nats://" + value;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) && !string.IsNullOrEmpty(uri.Host))
        {
            return (uri.Host, uri.Port > 0 ? uri.Port : 4222);
        }

        return ("127.0.0.1", 4222);
    }

    private void OnProcessOutput(object sender, DataReceivedEventArgs e)
    {
        LogNatsLine(e.Data, isErrorDefault: false);
    }

    private void OnProcessError(object sender, DataReceivedEventArgs e)
    {
        LogNatsLine(e.Data, isErrorDefault: true);
    }

    private void LogNatsLine(string? line, bool isErrorDefault)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        if (line.Contains("[INF]", StringComparison.Ordinal))
        {
            _logger.LogInformation("[NATS] {data}", line);
            return;
        }

        if (line.Contains("[WRN]", StringComparison.Ordinal))
        {
            _logger.LogWarning("[NATS] {data}", line);
            return;
        }

        if (line.Contains("[ERR]", StringComparison.Ordinal))
        {
            _logger.LogError("[NATS] {data}", line);
            return;
        }

        if (line.Contains("[FTL]", StringComparison.Ordinal))
        {
            _logger.LogCritical("[NATS] {data}", line);
            return;
        }

        if (line.Contains("[DBG]", StringComparison.Ordinal))
        {
            _logger.LogDebug("[NATS] {data}", line);
            return;
        }

        if (line.Contains("[TRC]", StringComparison.Ordinal))
        {
            _logger.LogTrace("[NATS] {data}", line);
            return;
        }

        if (isErrorDefault)
        {
            _logger.LogError("[NATS] {data}", line);
            return;
        }

        _logger.LogInformation("[NATS] {data}", line);
    }
}
