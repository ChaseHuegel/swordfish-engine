using System;
using System.Threading;
using WaywardBeyond.Data.Networking;

namespace WaywardBeyond.Client.Networking;

/// <summary>
/// Client-side view of the server's session heartbeats and ping: live TPS, player count, server sim
/// tick, and measured round-trip ping, plus staleness stamps so the F3 screen can show a clear
/// no-signal state instead of stale data.
/// </summary>
internal sealed class ServerStats
{
    private volatile uint _tps;
    private volatile uint _playerCount;
    private volatile uint _serverTick;
    private long _lastHeartbeatTicks;
    private double _pingMs;
    private long _lastPingTicks;

    public uint TPS => _tps;
    public uint PlayerCount => _playerCount;
    public uint ServerTick => _serverTick;
    /// <summary>The latest measured round-trip ping in milliseconds.</summary>
    public double PingMs => Volatile.Read(ref _pingMs);

    public void Record(in ServerHeartbeatMessage heartbeat)
    {
        _tps = heartbeat.TPS;
        _playerCount = heartbeat.PlayerCount;
        _serverTick = heartbeat.TickNumber;
        Interlocked.Exchange(ref _lastHeartbeatTicks, Environment.TickCount64);
    }

    /// <summary>Records a measured round-trip ping in milliseconds.</summary>
    public void RecordPing(double pingMs)
    {
        Volatile.Write(ref _pingMs, Math.Max(0d, pingMs));
        Interlocked.Exchange(ref _lastPingTicks, Environment.TickCount64);
    }

    /// <summary>Clears the recorded ping sample, e.g. on connection switch.</summary>
    public void ClearPing()
    {
        Interlocked.Exchange(ref _lastPingTicks, 0);
    }

    /// <summary>True when no heartbeat has arrived within <paramref name="staleMs"/>.</summary>
    public bool IsStale(int staleMs)
    {
        long last = Interlocked.Read(ref _lastHeartbeatTicks);
        return last == 0 || Environment.TickCount64 - last > staleMs;
    }

    /// <summary>True when no ping sample has arrived within <paramref name="staleMs"/>.</summary>
    public bool IsPingStale(int staleMs)
    {
        long last = Interlocked.Read(ref _lastPingTicks);
        return last == 0 || Environment.TickCount64 - last > staleMs;
    }
}