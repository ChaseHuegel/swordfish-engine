using Swordfish.Library.Configuration;
using Swordfish.Library.Types;

namespace WaywardBeyond.Shared.Config;

/// <summary>Host/join network settings: the LAN listen port (host) and the default join endpoint (client).</summary>
public sealed class NetworkingSettings : Config<NetworkingSettings>
{
    public DataBinding<int> ServerPort { get; private set; } = new(0);
    public DataBinding<string> DefaultHost { get; private set; } = new("127.0.0.1");
    public DataBinding<int> DefaultConnectPort { get; private set; } = new(7777);
}