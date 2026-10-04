using System;
using System.Numerics;
using Reef;
using Swordfish.Graphics;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Networking;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Client.Core.UI;

/// <summary>
/// F3 network section: per-second packets and bytes in/out (from the active transport's counters,
/// sampled into <see cref="Sampler"/>s), plain totals, and the live server TPS + player count from the
/// session-heartbeat consumer, with a clear no-signal state when heartbeats stop or nothing is
/// connected.
/// </summary>
internal sealed class NetworkStatsOverlay(in TransportManager transportManager, in ServerStats serverStats) : IDebugOverlay
{
    private const int SAMPLE_LENGTH = 60 * 2;

    private readonly TransportManager _transportManager = transportManager;
    private readonly ServerStats _serverStats = serverStats;

    private readonly Sampler _packetsInSampler = new(SAMPLE_LENGTH);
    private readonly Sampler _packetsOutSampler = new(SAMPLE_LENGTH);
    private readonly Sampler _bytesInSampler = new(SAMPLE_LENGTH);
    private readonly Sampler _bytesOutSampler = new(SAMPLE_LENGTH);

    private long _lastPacketsIn;
    private long _lastPacketsOut;
    private long _lastBytesIn;
    private long _lastBytesOut;

    public bool IsVisible() => true;

    public Result RenderDebugOverlay(double delta, UIBuilder<Material> ui)
    {
        IConnectionCounters? counters = _transportManager.Active as IConnectionCounters;
        if (counters == null)
        {
            using (ui.Text("NET: offline"))
            {
                ui.Color = new Vector4(1f, 0f, 0f, 1f);
            }
            return Result.FromSuccess();
        }

        double deltaSeconds = Math.Max(0.001, delta);
        long packetsIn = counters.PacketsReceived;
        long packetsOut = counters.PacketsSent;
        long bytesIn = counters.BytesReceived;
        long bytesOut = counters.BytesSent;

        _packetsInSampler.Record((packetsIn - _lastPacketsIn) / deltaSeconds);
        _packetsOutSampler.Record((packetsOut - _lastPacketsOut) / deltaSeconds);
        _bytesInSampler.Record((bytesIn - _lastBytesIn) / deltaSeconds);
        _bytesOutSampler.Record((bytesOut - _lastBytesOut) / deltaSeconds);
        _lastPacketsIn = packetsIn;
        _lastPacketsOut = packetsOut;
        _lastBytesIn = bytesIn;
        _lastBytesOut = bytesOut;

        Sample packetsInSample = _packetsInSampler.GetSnapshot();
        Sample packetsOutSample = _packetsOutSampler.GetSnapshot();
        Sample bytesInSample = _bytesInSampler.GetSnapshot();
        Sample bytesOutSample = _bytesOutSampler.GetSnapshot();

        using (ui.Text($"NET IN M:{packetsInSample.Median:F0} pkt/s / {Format(bytesInSample.Median)}/s")) { }
        using (ui.Text($"NET OUT M:{packetsOutSample.Median:F0} pkt/s / {Format(bytesOutSample.Median)}/s")) { }
        using (ui.Text($"NET TOTAL {Format(bytesIn)} in / {Format(bytesOut)} out")) { }

        //  Session heartbeat signal: live server TPS and player count, never stale.
        if (_serverStats.IsStale(2000))
        {
            using (ui.Text("NET: no server signal"))
            {
                ui.Color = new Vector4(1f, 0f, 0f, 1f);
            }
        }
        else
        {
            using (ui.Text($"NET: server TPS {_serverStats.TPS} / {_serverStats.PlayerCount} players / tick {_serverStats.ServerTick}")) { }
        }

        return Result.FromSuccess();
    }

    private static string Format(double bytesPerSecond)
    {
        if (bytesPerSecond >= 1024 * 1024)
        {
            return $"{bytesPerSecond / (1024 * 1024):F1} MiB";
        }

        if (bytesPerSecond >= 1024)
        {
            return $"{bytesPerSecond / 1024:F0} KiB";
        }

        return $"{bytesPerSecond:F0} B";
    }
}