using System;
using Swordfish.Library.Configuration;
using Swordfish.Library.Types;

namespace WaywardBeyond.Config;

/// <summary>Networking behavior configuration.</summary>
public sealed class NetworkingConfig : Config<NetworkingConfig>
{
    /// <summary>The host for new remote connections.</summary>
    public DataBinding<string> RemoteHost { get; private set; } = new("127.0.0.1");

    /// <summary>The port for new remote connections.</summary>
    public DataBinding<int> RemotePort { get; private set; } = new(7777);

    /// <inheritdoc cref="ServerConfig"/>
    public ServerConfig Server { get; private set; } = new();

    /// <inheritdoc cref="DiscoveryConfig"/>
    public DiscoveryConfig Discovery { get; private set; } = new();

    /// <inheritdoc cref="TransportConfig"/>
    public TransportConfig Transport { get; private set; } = new();

    /// <inheritdoc cref="ProtocolConfig"/>
    public ProtocolConfig Protocol { get; private set; } = new();

    /// <summary>Throws when the transport policy is inconsistent.</summary>
    public void Validate()
    {
        int queueBytes = Math.Max(1, Transport.SendQueueBytes.Get());
        int backlogBytes = Transport.SendBacklogDisconnectBytes.Get();
        int backlogMs = Transport.SendBacklogDisconnectMs.Get();

        if (backlogBytes > queueBytes)
        {
            throw new InvalidOperationException(
                $"Transport.SendBacklogDisconnectBytes ({backlogBytes}) must not exceed Transport.SendQueueBytes ({queueBytes}).");
        }

        if ((backlogBytes > 0) != (backlogMs > 0))
        {
            throw new InvalidOperationException(
                "Transport.SendBacklogDisconnectBytes and Transport.SendBacklogDisconnectMs must both be zero or both be positive.");
        }

        if (Protocol.InboundRatePerSecond.Get() < 1)
        {
            throw new InvalidOperationException("Protocol.InboundRatePerSecond must be at least 1.");
        }

        if (Protocol.InboundBurst.Get() < 1)
        {
            throw new InvalidOperationException("Protocol.InboundBurst must be at least 1.");
        }

        if (Protocol.InboundQueueDepth.Get() < 1)
        {
            throw new InvalidOperationException("Protocol.InboundQueueDepth must be at least 1.");
        }
    }
}
