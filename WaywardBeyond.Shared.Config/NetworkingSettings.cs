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
}