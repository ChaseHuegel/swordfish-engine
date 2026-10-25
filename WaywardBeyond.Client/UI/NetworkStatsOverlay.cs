using System;
using NsdRpc;
using Reef;
using Swordfish.Graphics;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Util;
using WaywardBeyond.Client.UI.Theme;
using WaywardBeyond.Client.Networking;

namespace WaywardBeyond.Client.UI;

/// <summary>
/// F3 network section: average per-second packets and bytes in/out (from the active RPC connection's
/// counters, sampled into <see cref="Sampler"/>s), plain totals, and the live server TPS + player count
/// from the session-heartbeat consumer, with a clear no-signal state when heartbeats stop or nothing is
/// connected. Rates use the average, not the median: network traffic is bursty, so the median of
/// per-frame samples reads zero whenever packets arrive in fewer frames than the render loop runs.
/// </summary>
internal sealed class NetworkStatsOverlay(in ClientRpcHost rpcHost, in ServerStats serverStats) : IDebugOverlay
{
    private const int SAMPLE_LENGTH = 60 * 2;

    private readonly ClientRpcHost _rpcHost = rpcHost;
    private readonly ServerStats _serverStats = serverStats;

    private readonly Sampler _packetsInSampler = new(SAMPLE_LENGTH);
    private readonly Sampler _packetsOutSampler = new(SAMPLE_LENGTH);
    private readonly Sampler _bytesInSampler = new(SAMPLE_LENGTH);
    private readonly Sampler _bytesOutSampler = new(SAMPLE_LENGTH);

    private long _lastPacketsIn;
    private long _lastPacketsOut;
    private long _lastBytesIn;
    private long _lastBytesOut;
    private bool _primed;

    public bool IsVisible() => true;

    public Result RenderDebugOverlay(double delta, UIBuilder<Material> ui)
    {
        if (!_rpcHost.IsConnected)
        {
            using (ui.Text("NET: offline"))
            {
                ui.Color = UiTheme.Danger;
            }
            return Result.FromSuccess();
        }

        RpcConnectionStats counters = _rpcHost.Stats;
        double deltaSeconds = Math.Max(0.001, delta);
        long packetsIn = counters.PacketsReceived;
        long packetsOut = counters.PacketsSent;
        long bytesIn = counters.BytesReceived;
        long bytesOut = counters.BytesSent;

        //  Seed from the live totals on the first render so the whole pre-existing count does not fold into one sample.
        if (_primed)
        {
            _packetsInSampler.Record((packetsIn - _lastPacketsIn) / deltaSeconds);
            _packetsOutSampler.Record((packetsOut - _lastPacketsOut) / deltaSeconds);
            _bytesInSampler.Record((bytesIn - _lastBytesIn) / deltaSeconds);
            _bytesOutSampler.Record((bytesOut - _lastBytesOut) / deltaSeconds);
        }

        _primed = true;
        _lastPacketsIn = packetsIn;
        _lastPacketsOut = packetsOut;
        _lastBytesIn = bytesIn;
        _lastBytesOut = bytesOut;

        Sample packetsInSample = _packetsInSampler.GetSnapshot();
        Sample packetsOutSample = _packetsOutSampler.GetSnapshot();
        Sample bytesInSample = _bytesInSampler.GetSnapshot();
        Sample bytesOutSample = _bytesOutSampler.GetSnapshot();

        using (ui.Text("NETWORK")) { }
        using (ui.Text($"   IN     A:{packetsInSample.Average:F0} pkt/s / {Format(bytesInSample.Average)}/s")) { }
        using (ui.Text($"   OUT    A:{packetsOutSample.Average:F0} pkt/s / {Format(bytesOutSample.Average)}/s")) { }
        using (ui.Text($"   BYTES  {Format(bytesIn)} in / {Format(bytesOut)} out")) { }
        using (ui.Text($"   PKTS   {packetsIn} in / {packetsOut} out")) { }

        //  Session heartbeat signal: live server TPS and player count, never stale.
        if (_serverStats.IsStale(2000))
        {
            using (ui.Text("No server signal"))
            {
                ui.Color = UiTheme.Danger;
            }
        }
        else
        {
            using (ui.Text($"   SERVER {_serverStats.TPS} tps / tick {_serverStats.ServerTick} / {_serverStats.PlayerCount} players")) { }
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
