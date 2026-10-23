namespace WaywardBeyond.Networking.Discovery;

/// <summary>
/// Shared constants for the LAN discovery beacon. The protocol version guards against mixed-version
/// hosts and clients; the broadcast address is the IPv4 limited-broadcast target every host and scanner
/// uses by agreement.
/// </summary>
public static class LanDiscovery
{
    public const int PROTOCOL_VERSION = 2;
    public const string BROADCAST_ADDRESS = "255.255.255.255";
}