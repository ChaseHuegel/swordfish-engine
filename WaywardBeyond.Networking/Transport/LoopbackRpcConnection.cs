using System;
using NsdRpc;

namespace WaywardBeyond.Networking.Transport;

/// <summary>The shared connection surface of an in-process loopback link. Not extensible outside this assembly.</summary>
public abstract class LoopbackRpcConnection : IRpcConnection, IRpcConnectionStats
{
    private readonly LoopbackRpcEndpoint _endpoint;

    private protected LoopbackRpcConnection(LoopbackRpcEndpoint endpoint)
    {
        _endpoint = endpoint;
    }

    /// <summary>The local endpoint this connection owns.</summary>
    internal LoopbackRpcEndpoint Endpoint => _endpoint;

    /// <inheritdoc/>
    public bool IsConnected => _endpoint.IsConnected;

    /// <inheritdoc/>
    public RpcConnectionStats Stats => _endpoint.Stats;

    /// <inheritdoc/>
    public void Close(RpcDisconnectReason reason = RpcDisconnectReason.LocalClose)
    {
        _endpoint.Close(reason);
    }

    /// <inheritdoc/>
    public bool TrySend(ReadOnlySpan<byte> frame)
    {
        return _endpoint.TrySend(frame);
    }

    /// <inheritdoc/>
    public event Action<RpcDisconnectReason> Disconnected
    {
        add => _endpoint.Disconnected += value;
        remove => _endpoint.Disconnected -= value;
    }

    /// <inheritdoc/>
    public event RpcFrameHandler FrameReceived
    {
        add => _endpoint.FrameReceived += value;
        remove => _endpoint.FrameReceived -= value;
    }
}
