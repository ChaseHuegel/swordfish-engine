using System.Collections.Concurrent;
using Swordfish.Library.Util;

namespace WaywardBeyond.Shared.Networking.Transport;

public sealed class LocalConnection : INetworkTransport
{
    private readonly ConcurrentQueue<object> _clientToServer = new();
    private readonly ConcurrentQueue<object> _serverToClient = new();

    private bool _isServer;

    public bool IsConnected => true;
    public bool IsLocal => true;

    public void MakeServer()
    {
        _isServer = true;
    }

    public Result Send<T>(in T message)
    {
        if (_isServer)
        {
            _serverToClient.Enqueue(message!);
        }
        else
        {
            _clientToServer.Enqueue(message!);
        }

        return Result.FromSuccess();
    }

    public Result<T> Receive<T>()
    {
        ConcurrentQueue<object> queue = _isServer ? _clientToServer : _serverToClient;

        if (queue.TryDequeue(out object? obj) && obj is T typed)
        {
            return Result<T>.FromSuccess(typed);
        }

        return Result<T>.FromFailure("No messages available.");
    }
}
