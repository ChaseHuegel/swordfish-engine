using Swordfish.Library.Configuration;
using Swordfish.Library.Types;

namespace WaywardBeyond.Config;

/// <summary>Networking behavior configuration.</summary>
public sealed class NetworkingConfig : Config<NetworkingConfig>
{
    /// <summary>The port to host the server on. A value of 0 picks any available port.</summary>
    public DataBinding<int> ServerPort { get; private set; } = new(0);

    /// <summary>The name of the server.</summary>
    public DataBinding<string> ServerName { get; private set; } = new("LAN Server");
    
    /// <summary>The host for new remote connections.</summary>
    public DataBinding<string> RemoteHost { get; private set; } = new("127.0.0.1");
    
    /// <summary>The port for new remote connections.</summary>
    public DataBinding<int> RemotePort { get; private set; } = new(7777);
    
    /// <summary>
    /// Whether broadcasting the server for LAN discovery is enabled.
    /// </summary>
    public DataBinding<bool> DiscoveryBroadcasting { get; private set; } = new(true);
    
    /// <summary>The port to use for LAN discovery.</summary>
    public DataBinding<int> DiscoveryPort { get; private set; } = new(47777);
    
    /// <summary>The rate in seconds at which to broadcast the server for LAN discovery.</summary>
    public DataBinding<int> DiscoveryBroadcastSeconds { get; private set; } = new(5);
    
    /// <summary>The duration in seconds at which to scan for LAN servers.</summary>
    public DataBinding<int> DiscoveryScanDurationSeconds { get; private set; } = new(20);
    
    /// <summary>Bounded socket read/write timeout in milliseconds for a peer connection.</summary>
    public DataBinding<int> ConnectionTimeoutMs { get; private set; } = new(5000);
    
    /// <summary>App-level session heartbeat interval in milliseconds; doubles as the transport keepalive (clamped below the connection timeout).</summary>
    public DataBinding<int> HeartbeatIntervalMs { get; private set; } = new(1000);
    
    /// <summary>Sim-tick lag past which a client's reported tick logs a warn.</summary>
    public DataBinding<int> TickLagWarnThreshold { get; private set; } = new(10);
    
    /// <summary>ECS snapshot cadence per second: the server publish rate and the client upload rate.</summary>
    public DataBinding<int> SnapshotHz { get; private set; } = new(30);
    
    /// <summary>How often the send thread drains both queues into coalesced socket writes, in milliseconds (clamped to one snapshot interval).</summary>
    public DataBinding<int> SendIntervalMs { get; private set; } = new(16);
    
    /// <summary>Maximum pending send frames a peer transport buffers before dropping the oldest.</summary>
    public DataBinding<int> SendQueueSize { get; private set; } = new(256);
    
    /// <summary>Maximum frame body a peer transport accepts or sends, in bytes.</summary>
    public DataBinding<int> MaxFrameBytes { get; private set; } = new(16 * 1024 * 1024);
    
    /// <summary>Reliable send queue length that triggers an error log when exceeded (re-logged every ~100 frames).</summary>
    public DataBinding<int> ReliableQueueConcernThreshold { get; private set; } = new(64);
    
    /// <summary>Reliable send backlog length that gets a peer disconnected when held past the disconnect window.</summary>
    public DataBinding<int> ReliableQueueDisconnectThreshold { get; private set; } = new(128);
    
    /// <summary>How long a peer may hold the reliable backlog over the disconnect threshold before it is dropped, in milliseconds.</summary>
    public DataBinding<int> ReliableQueueDisconnectMs { get; private set; } = new(10_000);
    
    /// <summary>Client-side join timeout: how long a join may wait for the world stream to complete, in milliseconds.</summary>
    public DataBinding<int> JoinStreamTimeoutMs { get; private set; } = new(60_000);
    
    /// <summary>Frames drained per client per hub poll; bounds a chatty client's share of the server tick.</summary>
    public DataBinding<int> MaxReceiveWindow { get; private set; } = new(10);
    
    /// <summary>How long a world with no sessions stays loaded before it unloads, in milliseconds.</summary>
    public DataBinding<int> WorldIdleUnloadMs { get; private set; } = new(60_000);
    
    /// <summary>Logs every sent and received frame (type, byte count, direction) at trace level.</summary>
    public DataBinding<bool> TraceLogging { get; private set; } = new(false);
}
