using System.Collections.Concurrent;
using WaywardBeyond.Networking.Transport;
using WaywardBeyond.Permissions;

namespace WaywardBeyond.Server.Permissions;

/// <summary>
///     Pre-join claims keyed by connection. The client sends its claim once in a <c>ClientHello</c>, so
///     menu-time requests can resolve the user without carrying an id on every message. Entries live for
///     the connection's lifetime and are cleared on disconnect.
/// </summary>
public sealed class ConnectionClaims
{
    private readonly ConcurrentDictionary<IServerConnection, UserClaim> _claims = new();

    public void Bind(IServerConnection connection, in UserClaim claim)
    {
        _claims[connection] = claim;
    }

    public bool TryGet(IServerConnection connection, out UserClaim claim)
    {
        return _claims.TryGetValue(connection, out claim);
    }

    public void Clear(IServerConnection connection)
    {
        _claims.TryRemove(connection, out _);
    }
}
