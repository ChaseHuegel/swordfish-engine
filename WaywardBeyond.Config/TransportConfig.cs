using Swordfish.Library.Types;

namespace WaywardBeyond.Config;

/// <summary>Network transport configuration.</summary>
public sealed record TransportConfig
{
    /// <summary>Logs every sent and received frame (type, byte count, direction) at trace level.</summary>
    public DataBinding<bool> TraceLogging { get; private set; } = new(false);
    
    /// <summary>Bounded socket read/write timeout in milliseconds for a peer connection.</summary>
    public DataBinding<int> TimeoutMs { get; private set; } = new(5000);
    
    /// <summary>Maximum frame body a peer transport accepts or sends, in bytes.</summary>
    public DataBinding<int> MaxFrameBytes { get; private set; } = new(16 * 1024 * 1024);
    
    /// <summary>How often the send thread drains both queues into coalesced socket writes, in milliseconds (clamped to one snapshot interval).</summary>
    public DataBinding<int> SendIntervalMs { get; private set; } = new(16);
    
    /// <summary>Maximum pending send frames a peer transport buffers before dropping the oldest.</summary>
    public DataBinding<int> SendQueueSize { get; private set; } = new(256);
    
    /// <summary>Reliable send queue length that triggers an error log when exceeded.</summary>
    public DataBinding<int> ReliableQueueConcernThreshold { get; private set; } = new(64);
    
    /// <summary>Reliable send backlog length that gets a peer disconnected when held past the disconnect window.</summary>
    public DataBinding<int> ReliableQueueDisconnectThreshold { get; private set; } = new(128);
    
    /// <summary>How long a peer may hold the reliable backlog over the disconnect threshold before it is dropped, in milliseconds.</summary>
    public DataBinding<int> ReliableQueueDisconnectMs { get; private set; } = new(10_000);
}