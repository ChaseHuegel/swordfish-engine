using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Config;

namespace WaywardBeyond.Server.Core.Streaming;

/// <summary>
///     Starts and manages a NATS server, ensuring it stays running.
/// </summary>
internal sealed class PersistentNatsProcess : IDisposable
{
    private const string VAR_NATS_ARGS = "NATS_ARGS";

    private readonly Lock _lock = new();
    private readonly ProcessStartInfo _startInfo;
    
    private Process? _process;

    public PersistentNatsProcess(in VirtualFileSystem vfs, in IConfiguration configuration)
    {
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
        
        _startInfo = new ProcessStartInfo(absolutePath.Value)
        {
            Arguments = configuration.GetString(VAR_NATS_ARGS),
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
    }
    
    public void Dispose()
    {
        using Lock.Scope _ = _lock.EnterScope();
        _process?.Dispose();
    }
    
    public Result Start()
    {
        using Lock.Scope _ = _lock.EnterScope();
        
        if (_process != null)
        {
            return Result.FromSuccess();
        }
        
        Process? process = Process.Start(_startInfo);
        if (process == null || process.HasExited)
        {
            return Result.FromFailure($"NATS server process failed to start at \"{_startInfo.FileName}\".");
        }
        
        _process = process;
        _process.Exited += OnProcessExited;
        
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
        }

        //  Restart the process
        Process? process = Process.Start(_startInfo);
        if (process == null || process.HasExited)
        {
            throw new InvalidOperationException("NATS server process failed to restart.");
        }

        _process = process;
        _process.Exited += OnProcessExited;
    }
}