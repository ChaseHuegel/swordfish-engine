using System;
using System.Threading.Tasks;
using NsdRpc;
using Swordfish.Library.Util;

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

    /// <summary>Connects to the loopback transport without throwing when the listener is closed.</summary>
    public Result Connect()
    {
        if (IsConnected)
        {
            return Result.FromFailure("The loopback connection is already connected.");
        }

        return _transport.TryConnect(Endpoint);
    }

    /// <inheritdoc/>
    public Task ConnectAsync()
    {
        Result result = Connect();
        if (!result.Success)
        {
            throw new InvalidOperationException(result.Message);
        }

        return Task.CompletedTask;
    }
}
