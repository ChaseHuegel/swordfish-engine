using System.Collections.Concurrent;
using System.Collections.Generic;
using Swordfish.ECS;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Networking;

namespace WaywardBeyond.Server.Core;

/// <summary>
/// The per-world inbound queue of pre-routed joins. The world host consumes a connection's
/// <c>JoinRequest</c> to learn which world it targets, binds the connection to that world's hub, and
/// enqueues the request here so the world's join system processes it exactly once.
/// </summary>
public sealed class ServerJoinQueue
{
    private readonly ConcurrentQueue<(Uuid clientId, JoinRequest request)> _joins = new();

    public void Enqueue(Uuid clientId, in JoinRequest request)
    {
        _joins.Enqueue((clientId, request));
    }

    public IEnumerable<(Uuid clientId, JoinRequest request)> Drain()
    {
        while (_joins.TryDequeue(out (Uuid clientId, JoinRequest request) join))
        {
            yield return join;
        }
    }
}