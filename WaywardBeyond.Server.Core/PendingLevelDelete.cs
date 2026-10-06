using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Server.Core;

/// <summary>One level-delete request awaiting the server thread and the connection to answer.</summary>
public readonly struct PendingLevelDelete(in IServerConnection connection, in string levelGuid)
{
    public readonly IServerConnection Connection = connection;
    public readonly string LevelGuid = levelGuid;
}
