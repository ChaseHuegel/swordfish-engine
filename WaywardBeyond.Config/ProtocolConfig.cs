using Swordfish.Library.Types;

namespace WaywardBeyond.Config;

/// <summary>Network protocol configuration.</summary>
public sealed record ProtocolConfig
{
    /// <summary>App-level session heartbeat interval in milliseconds; doubles as the transport keepalive (clamped below the connection timeout).</summary>
    public DataBinding<int> HeartbeatIntervalMs { get; private set; } = new(1000);
    
    /// <summary>ECS snapshot cadence per second: the server publish rate and the client upload rate.</summary>
    public DataBinding<int> SnapshotHz { get; private set; } = new(30);
    
    /// <summary>How long a client-side join may wait for the level stream to complete, in milliseconds.</summary>
    public DataBinding<int> JoinStreamTimeoutMs { get; private set; } = new(60_000);

    /// <summary>The default RPC call deadline, in milliseconds. Zero disables it.</summary>
    public DataBinding<int> CallTimeoutMs { get; private set; } = new(30_000);

    /// <summary>The most pending calls and inbound requests one RPC peer tracks.</summary>
    public DataBinding<int> MaxPendingCalls { get; private set; } = new(1_024);

    /// <summary>The most chunks one RPC stream session buffers.</summary>
    public DataBinding<int> MaxBufferedChunks { get; private set; } = new(1_024);

    /// <summary>The level-stream bytes one server tick may enqueue per stream.</summary>
    public DataBinding<int> LevelStreamBytesPerTick { get; private set; } = new(262_144);

    /// <summary>The sustained inbound messages per second one client may send before the server closes it.</summary>
    public DataBinding<int> InboundRatePerSecond { get; private set; } = new(240);

    /// <summary>The inbound message burst one client may send before the rate budget applies.</summary>
    public DataBinding<int> InboundBurst { get; private set; } = new(480);

    /// <summary>The most messages one client may have staged for a world tick, per queue.</summary>
    public DataBinding<int> InboundQueueDepth { get; private set; } = new(256);
}