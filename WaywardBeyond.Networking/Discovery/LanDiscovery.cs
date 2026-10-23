namespace WaywardBeyond.Networking.Discovery;

/// <summary>Shared constants for the LAN discovery beacon.</summary>
public static class LanDiscovery
{
    /// <summary>The beacon protocol version. Hosts and clients must match.</summary>
    public const int PROTOCOL_VERSION = 3;

    /// <summary>The IPv4 limited-broadcast address every host and scanner uses.</summary>
    public const string BROADCAST_ADDRESS = "255.255.255.255";
}
