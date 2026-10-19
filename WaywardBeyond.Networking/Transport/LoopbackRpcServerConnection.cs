using System;
using System.Net;
using NsdRpc;
using RpcServerConnection = NsdRpc.IServerConnection;

namespace WaywardBeyond.Networking.Transport;

/// <summary>
/// The server half of an in-process loopback link. The server transport assigns the client id at
/// accept. There is no socket, so <see cref="RemoteEndPoint"/> is null.
/// </summary>
public sealed class LoopbackRpcServerConnection : RpcServerConnection, IRpcConnectionStats
{
    private readonly LoopbackRpcServerTransport _transport;
    private readonly LoopbackRpcEndpoint _endpoint;

    internal LoopbackRpcServerConnection(
        ulong clientId,
        LoopbackRpcEndpoint endpoint,
        LoopbackRpcServerTransport transport
    ) {
        ClientId = clientId;
        _endpoint = endpoint;
        _transport = transport;
        _endpoint.Disconnected += _ => _transport.Remove(this);
    }

    public ulong ClientId { get; }

    public EndPoint? RemoteEndPoint => null;

    public bool IsConnected => _endpoint.IsConnected;

    public RpcConnectionStats Stats => _endpoint.Stats;

    public void Close(RpcDisconnectReason reason = RpcDisconnectReason.LocalClose)
    {
        _endpoint.Close(reason);
    }

    public bool TrySend(ReadOnlySpan<byte> frame)
    {
        return _endpoint.TrySend(frame);
    }

    public event Action<RpcDisconnectReason> Disconnected
    {
        add => _endpoint.Disconnected += value;
        remove => _endpoint.Disconnected -= value;
    }

    public event RpcFrameHandler FrameReceived
    {
        add => _endpoint.FrameReceived += value;
        remove => _endpoint.FrameReceived -= value;
    }
}
