namespace WaywardBeyond.Networking.Discovery;

/// <summary>Live host metadata shared between the server host, LAN beacon, and discovery handlers.</summary>
public sealed class LanHostInfo
{
    private volatile int _listenPort;
    private volatile int _playerCount;

    /// <summary>The bound TCP port, or zero when this process does not host a server.</summary>
    public int ListenPort
    {
        get => _listenPort;
        set => _listenPort = value;
    }

    /// <summary>The live player count across all hosted worlds.</summary>
    public int PlayerCount
    {
        get => _playerCount;
        set => _playerCount = value;
    }
}
