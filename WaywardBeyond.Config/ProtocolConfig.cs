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
}