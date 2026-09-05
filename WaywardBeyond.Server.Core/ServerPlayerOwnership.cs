using System.Threading;
using Swordfish.ECS;

namespace WaywardBeyond.Server.Core;

/// <summary>
/// Tracks which server-spawned entity belongs to the single connected client. Until multi-client
/// session routing lands, the server echoes the owner's placed transform back, so this holder lets the
/// replication system skip re-publishing it to the owner (client-side motion is authoritative locally).
/// </summary>
public sealed class ServerPlayerOwnership
{
    private readonly Lock _lock = new();
    private Uuid? _ownedPlayer;

    public void SetOwnedPlayer(Uuid uuid)
    {
        lock (_lock)
        {
            _ownedPlayer = uuid;
        }
    }

    public Uuid? GetOwnedPlayer()
    {
        lock (_lock)
        {
            return _ownedPlayer;
        }
    }
}