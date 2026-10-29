using System;
using System.Threading;
using Shoal.DependencyInjection;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Config;
using WaywardBeyond.Data.Networking;

namespace WaywardBeyond.Client.Networking;

/// <summary>
/// Session heartbeats, client side. Sends <see cref="ClientHeartbeatMessage"/> at a timer-driven cadence,
/// clamped below the idle timeout, so the heartbeat doubles as the transport keepalive.
/// </summary>
internal sealed class ClientHeartbeatService : IDisposable, IAutoActivate
{
    private readonly ClientRpcHost _host;
    private readonly SnapshotAckTracker _snapshotAck;
    private readonly ClientPlayerMotionProcessor _motionProcessor;
    private readonly Timer _timer;
    private bool _disposed;

    /// <summary>Creates the timer-driven client heartbeat service.</summary>
    public ClientHeartbeatService(
        in ClientRpcHost host,
        in SnapshotAckTracker snapshotAck,
        in ClientPlayerMotionProcessor motionProcessor,
        in NetworkingConfig config
    ) {
        _host = host;
        _snapshotAck = snapshotAck;
        _motionProcessor = motionProcessor;
        int intervalMs = GetServerHeartbeatInterval(config);
        _timer = new Timer(OnHeartbeatTick, null, intervalMs, intervalMs);
    }

    private void OnHeartbeatTick(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _host.Api?.ClientHeartbeat(new ClientHeartbeatMessage
            {
                TickNumber = _motionProcessor.Step?.CurrentSimTick ?? 0,
                LastAppliedSnapshotTick = _snapshotAck.LastAppliedSnapshotTick,
            });
        }
        catch
        {
            //  A failed heartbeat send during disconnection is non-fatal.
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Dispose();
    }

    internal static int GetServerHeartbeatInterval(NetworkingConfig config)
    {
        int requested = config.Protocol.HeartbeatIntervalMs.Get();
        int idle = config.Transport.IdleTimeoutMs.Get();
        return idle > 0 ? Math.Max(250, Math.Min(requested, idle / 2)) : Math.Max(250, requested);
    }
}
