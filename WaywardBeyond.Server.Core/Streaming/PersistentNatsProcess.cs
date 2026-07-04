using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Server.Core.Interop.Windows;
using WaywardBeyond.Shared.Config;

namespace WaywardBeyond.Server.Core.Streaming;

/// <summary>
///     Starts and manages a NATS server, ensuring it stays running.
/// </summary>
public sealed class PersistentNatsProcess : IDisposable
{
    private readonly ILogger<PersistentNatsProcess> _logger;
    private const string VAR_NATS_EXTRA_ARGS = "NATS_EXTRA_ARGS";

    private readonly Lock _lock = new();
    private readonly ProcessStartInfo _startInfo;
    
    private Process? _process;
    private Job? _windowsJob;

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

        string storageDirectory = Path.GetFullPath("saves/").Replace('\\', '/');
        
        _startInfo = new ProcessStartInfo(absolutePath.Value)
        {
            Arguments = $"-js -sd \"{storageDirectory}\" {configuration.GetString(VAR_NATS_EXTRA_ARGS)}",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
    }
    
    public void Dispose()
    {
        using Lock.Scope _ = _lock.EnterScope();

        _windowsJob?.Dispose();
        _process?.Dispose();
    }
    
    public Result Start()
    {
        using Lock.Scope _ = _lock.EnterScope();
        
        if (_process != null)
        {
            return Result.FromSuccess();
        }

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

            if (_process.HasExited)
            {
                return Result.FromFailure($"NATS server process failed to start at \"{_startInfo.FileName}\".");
            }

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
    
    private void OnProcessExited(object? sender, EventArgs e)
    {
        using Lock.Scope _ = _lock.EnterScope();
        
        //  Cleanup the previous process
        if (_process != null)
        {
            _process.Exited -= OnProcessExited;
            _process.Dispose();
            
            _windowsJob?.Dispose();
        }

        //  Restart the process
        Process? process = Process.Start(_startInfo);
        if (process == null || process.HasExited)
        {
            throw new InvalidOperationException("NATS server process failed to restart.");
        }

        _process = process;
        _process.OutputDataReceived += OnProcessOutput;
        _process.ErrorDataReceived += OnProcessError;
        _process.Exited += OnProcessExited;
        
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
        
        if (OperatingSystem.IsWindows())
        {
            _windowsJob = new Job();
            _windowsJob.AddProcess(process.Handle);
        }
    }
    
    private void OnProcessOutput(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(e.Data))
        {
            _logger.LogInformation("[NATS] {data}", e.Data);
        }
    }

    private void OnProcessError(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(e.Data))
        {
            _logger.LogError("[NATS ERROR] {data}", e.Data);
        }
    }
}
