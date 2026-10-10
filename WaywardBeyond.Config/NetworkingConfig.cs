using Swordfish.Library.Configuration;
using Swordfish.Library.Types;

namespace WaywardBeyond.Config;

/// <summary>Networking behavior configuration.</summary>
public sealed class NetworkingConfig : Config<NetworkingConfig>
{
    /// <inheritdoc cref="ServerConfig"/>
    public ServerConfig Server { get; private set; } = new();
    
    /// <inheritdoc cref="DiscoveryConfig"/>
    public DiscoveryConfig Discovery { get; private set; } = new();
    
    /// <inheritdoc cref="TransportConfig"/>
    public TransportConfig Transport { get; private set; } = new();
    
    /// <inheritdoc cref="ProtocolConfig"/>
    public ProtocolConfig Protocol { get; private set; } = new();
    
    /// <summary>The host for new remote connections.</summary>
    public DataBinding<string> RemoteHost { get; private set; } = new("127.0.0.1");
    
    /// <summary>The port for new remote connections.</summary>
    public DataBinding<int> RemotePort { get; private set; } = new(7777);
}
