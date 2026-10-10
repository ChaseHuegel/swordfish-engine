using Swordfish.Library.Types;

namespace WaywardBeyond.Config;

/// <summary>LAN discovery configuration.</summary>
public sealed record DiscoveryConfig
{
    /// <summary>Whether broadcasting the server for LAN discovery is enabled.</summary>
    public DataBinding<bool> Enabled { get; private set; } = new(true);
    
    /// <summary>The port to use for LAN discovery.</summary>
    public DataBinding<int> Port { get; private set; } = new(47777);
    
    /// <summary>The rate in seconds at which to broadcast the server for LAN discovery.</summary>
    public DataBinding<int> BroadcastSeconds { get; private set; } = new(5);
    
    /// <summary>The duration in seconds at which to scan for LAN servers.</summary>
    public DataBinding<int> ScanDurationSeconds { get; private set; } = new(20);
}