using Swordfish.Library.Types;

namespace WaywardBeyond.Config;

/// <summary>Server configuration.</summary>
public sealed record ServerConfig
{
    /// <summary>The port to host the server on. A value of 0 picks any available port.</summary>
    public DataBinding<int> Port { get; private set; } = new(0);

    /// <summary>The name of the server.</summary>
    public DataBinding<string> Name { get; private set; } = new("LAN Server");
    
    /// <summary>How long until a host with no sessions is unloaded, in milliseconds.</summary>
    public DataBinding<int> IdleUnloadMs { get; private set; } = new(60_000);
    
    /// <summary>Sim-tick lag past which a client's reported tick logs a warn.</summary>
    public DataBinding<int> TickLagWarnThreshold { get; private set; } = new(10);
    
    /// <summary>Frames drained per client per hub poll; bounds a chatty client's share of the server tick.</summary>
    public DataBinding<int> MaxReceiveWindow { get; private set; } = new(10);
}