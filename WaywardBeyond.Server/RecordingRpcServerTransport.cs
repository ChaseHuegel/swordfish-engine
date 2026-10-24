using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using NsdRpc;

namespace WaywardBeyond.Server;

/// <summary>
/// Wraps an <see cref="IRpcServerTransport"/> and records each accepted connection's disconnect
/// reason by client id. It returns the original connection, so the nsd-rpc peer keeps the owned-frame
/// send path.
/// </summary>
public sealed class RecordingRpcServerTransport : IRpcServerTransport
{
    private readonly IRpcServerTransport _inner;
    private readonly ConcurrentDictionary<ulong, RpcDisconnectReason> _reasons = new();

    /// <summary>Wraps <paramref name="inner"/> so accepted connections record their disconnect reason.</summary>
    public RecordingRpcServerTransport(IRpcServerTransport inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    /// <inheritdoc/>
    public async Task<IServerConnection?> AcceptAsync()
    {
        IServerConnection? connection = await _inner.AcceptAsync().ConfigureAwait(false);
        if (connection == null)
        {
            return null;
        }

        ulong clientId = connection.ClientId;
        connection.Disconnected += reason => _reasons[clientId] = reason;
        return connection;
    }

    /// <summary>Removes and returns the recorded reason for a client.</summary>
    public bool TryTakeReason(ulong clientId, out RpcDisconnectReason reason)
    {
        return _reasons.TryRemove(clientId, out reason);
    }

    /// <inheritdoc/>
    public void Close()
    {
        _inner.Close();
    }
}
