using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NsdRpc;
using Shoal.DependencyInjection;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Config;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Networking.Rpc;

namespace WaywardBeyond.Client.Networking;

/// <summary>Sends periodic session heartbeats and measures round-trip ping for the active connection.</summary>
internal sealed class ClientHeartbeatService : IDisposable, IAutoActivate
{
    private readonly ClientRpcHost _host;
    private readonly SnapshotAckTracker _snapshotAck;
    private readonly ClientPlayerMotionProcessor? _motionProcessor;
    private readonly ServerStats _serverStats;
    private readonly int _pingTimeoutMs;
    private readonly Timer _timer;
    private GameServerApiClient? _lastApi;
    private int _pingInFlight;
    private bool _disposed;

    /// <summary>Creates the timer-driven client heartbeat service.</summary>
    public ClientHeartbeatService(
        in ClientRpcHost host,
        in SnapshotAckTracker snapshotAck,
        in ClientPlayerMotionProcessor? motionProcessor,
        in NetworkingConfig config,
        in ServerStats serverStats
    ) {
        _host = host;
        _snapshotAck = snapshotAck;
        _motionProcessor = motionProcessor;
        _serverStats = serverStats;
        _pingTimeoutMs = Math.Max(250, config.Discovery.PingTimeoutMs.Get());
        int intervalMs = GetServerHeartbeatInterval(config);
        _timer = new Timer(OnHeartbeatTick, null, intervalMs, intervalMs);
    }

    private void OnHeartbeatTick(object? state)
    {
        if (_disposed)
        {
            return;
        }

        GameServerApiClient? api = _host.Api;
        if (!ReferenceEquals(api, _lastApi))
        {
            _lastApi = api;
            _serverStats.ClearPing();
        }

        if (api == null)
        {
            return;
        }

        try
        {
            api.ClientHeartbeat(new ClientHeartbeatMessage
            {
                TickNumber = _motionProcessor?.Step?.CurrentSimTick ?? 0,
                LastAppliedSnapshotTick = _snapshotAck.LastAppliedSnapshotTick,
            });
        }
        catch
        {
            //  A failed heartbeat send during disconnection is non-fatal.
        }

        if (Interlocked.CompareExchange(ref _pingInFlight, 1, 0) == 0)
        {
            _ = MeasurePingAsync(api);
        }
    }

    private async Task MeasurePingAsync(GameServerApiClient api)
    {
        try
        {
            using var cts = new CancellationTokenSource(_pingTimeoutMs);
            long started = Stopwatch.GetTimestamp();
            RpcResult<ServerDiscovery> response = await api
                .DiscoverAsync(new ServerDiscoveryRequest(), cts.Token)
                .WaitAsync(cts.Token)
                .ConfigureAwait(false);

            if (response.IsSuccess && !_disposed && ReferenceEquals(_host.Api, api))
            {
                _serverStats.RecordPing(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }
        }
        catch
        {
            //  A failed or timed-out ping during disconnection is non-fatal.
        }
        finally
        {
            Interlocked.Exchange(ref _pingInFlight, 0);
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
