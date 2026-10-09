using System.Collections.Concurrent;
using WaywardBeyond.Networking.Transport;

namespace WaywardBeyond.Server;

/// <summary>
/// Level-delete requests deferred from the menu facade to the server thread. A delete of a loaded level
/// must tear the level down on the thread that owns it, including any connected players, before its
/// files are removed.
/// </summary>
public sealed class PendingLevelDeletes
{
    private readonly ConcurrentQueue<PendingLevelDelete> _deletes = new();

    public void Enqueue(in IServerConnection connection, string levelGuid)
    {
        _deletes.Enqueue(new PendingLevelDelete(connection, levelGuid));
    }

    public bool TryDequeue(out PendingLevelDelete delete)
    {
        return _deletes.TryDequeue(out delete);
    }
}
