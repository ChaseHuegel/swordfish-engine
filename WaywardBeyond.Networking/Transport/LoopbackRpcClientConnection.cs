using System;
using System.Threading.Tasks;
using NsdRpc;
using RpcClientConnection = NsdRpc.IClientConnection;

namespace WaywardBeyond.Networking.Transport;

/// <summary>
/// The client half of an in-process loopback link. Connect it before passing it to an
/// <see cref="RpcClient"/>. A closed instance is not reusable. Create a fresh one to restore the
/// loopback session.
/// </summary>
public sealed class LoopbackRpcClientConnection(LoopbackRpcServerTransport transport) : RpcClientConnection, IRpcConnectionStats
{
    private readonly LoopbackRpcServerTransport _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    private readonly LoopbackRpcEndpoint _endpoint = new();

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

    public Task ConnectAsync()
    {
        if (IsConnected)
        {
            throw new InvalidOperationException("The loopback connection is already connected.");
        }

        _transport.Connect(_endpoint);
        return Task.CompletedTask;
    }
}
