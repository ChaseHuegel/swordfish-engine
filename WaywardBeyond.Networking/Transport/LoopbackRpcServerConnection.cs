using System.Net;
using NsdRpc;

namespace WaywardBeyond.Networking.Transport;

/// <summary>The server half of an in-process loopback link. There is no socket, so the remote endpoint is null.</summary>
public sealed class LoopbackRpcServerConnection : LoopbackRpcConnection, IServerConnection
{
    private readonly LoopbackRpcServerTransport _transport;

    internal LoopbackRpcServerConnection(ulong clientId, LoopbackRpcEndpoint endpoint, LoopbackRpcServerTransport transport)
        : base(endpoint)
    {
        ClientId = clientId;
        _transport = transport;
        Endpoint.Disconnected += _ => _transport.Remove(this);
    }

    /// <inheritdoc/>
    public ulong ClientId { get; }

    /// <inheritdoc/>
    public EndPoint? RemoteEndPoint => null;
}
