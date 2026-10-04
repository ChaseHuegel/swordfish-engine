using Swordfish.Library.Configuration;
using Swordfish.Library.Types;

namespace WaywardBeyond.Shared.Config;

/// <summary>
/// Host/join network settings: the LAN listen port (host), the default join endpoint (client), and the
/// LAN discovery beacon configuration (host advertiser + client scanner).
/// </summary>
public sealed class NetworkingSettings : Config<NetworkingSettings>
{
    public DataBinding<int> ServerPort { get; private set; } = new(0);
    public DataBinding<string> DefaultHost { get; private set; } = new("127.0.0.1");
    public DataBinding<int> DefaultConnectPort { get; private set; } = new(7777);
    public DataBinding<string> ServerName { get; private set; } = new("LAN Server");
    public DataBinding<int> DiscoveryPort { get; private set; } = new(47777);
    public DataBinding<bool> LanDiscovery { get; private set; } = new(true);
    public DataBinding<int> DiscoveryBroadcastSeconds { get; private set; } = new(5);
    public DataBinding<int> DiscoveryScanSeconds { get; private set; } = new(20);
    /// <summary>Bounded socket read/write timeout in milliseconds for a peer connection.</summary>
    public DataBinding<int> ConnectionTimeoutMs { get; private set; } = new(5000);
    /// <summary>App-level session heartbeat interval in milliseconds; doubles as the transport keepalive (clamped below the connection timeout).</summary>
    public DataBinding<int> HeartbeatIntervalMs { get; private set; } = new(1000);
    /// <summary>Sim-tick lag past which a client's reported tick logs a warn.</summary>
    public DataBinding<int> TickLagWarnThreshold { get; private set; } = new(10);
    /// <summary>World snapshot publishes per second (server publish cadence).</summary>
    public DataBinding<int> SnapshotHz { get; private set; } = new(30);
    /// <summary>How often the TCP send thread drains both queues into coalesced socket writes, in milliseconds (clamped to one snapshot interval).</summary>
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
    /// <summary>Logs every sent and received frame (type, byte count, direction) at trace level.</summary>
    public DataBinding<bool> TraceLogging { get; private set; } = new(false);
}
