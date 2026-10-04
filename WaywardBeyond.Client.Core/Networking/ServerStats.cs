using System;
using System.Threading;
using WaywardBeyond.Shared.Networking;

namespace WaywardBeyond.Client.Core.Networking;

/// <summary>
/// Client-side view of the server's session heartbeats: live TPS, player count, and the server's sim
/// tick, plus a staleness stamp so the F3 screen can show a clear no-signal state instead of stale
/// data.
/// </summary>
internal sealed class ServerStats
{
    private volatile uint _tps;
    private volatile uint _playerCount;
    private volatile uint _serverTick;
    private long _lastHeartbeatTicks;

    public uint TPS => _tps;
    public uint PlayerCount => _playerCount;
    public uint ServerTick => _serverTick;

    public void Record(in ServerHeartbeatMessage heartbeat)
    {
        _tps = heartbeat.TPS;
        _playerCount = heartbeat.PlayerCount;
        _serverTick = heartbeat.TickNumber;
        Interlocked.Exchange(ref _lastHeartbeatTicks, Environment.TickCount);
    }

    /// <summary>True when no heartbeat has arrived within <paramref name="staleMs"/>.</summary>
    public bool IsStale(int staleMs)
    {
        long last = Interlocked.Read(ref _lastHeartbeatTicks);
        return last == 0 || Environment.TickCount - last > staleMs;
    }
}