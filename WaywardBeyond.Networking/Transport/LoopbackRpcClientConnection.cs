using System;
using System.Threading.Tasks;
using NsdRpc;

namespace WaywardBeyond.Networking.Transport;

/// <summary>The client half of an in-process loopback link. A closed instance is not reusable.</summary>
public sealed class LoopbackRpcClientConnection : LoopbackRpcConnection, IClientConnection
{
    private readonly LoopbackRpcServerTransport _transport;

    /// <summary>Creates a client connection that pairs with <paramref name="transport"/> on connect.</summary>
    public LoopbackRpcClientConnection(LoopbackRpcServerTransport transport)
        : base(new LoopbackRpcEndpoint())
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    /// <inheritdoc/>
    /// <inheritdoc/>
    public Task ConnectAsync()
    {
        if (IsConnected)
        {
            throw new InvalidOperationException("The loopback connection is already connected.");
        }

        _transport.Connect(Endpoint);
        return Task.CompletedTask;
    }
}
