namespace WaywardBeyond.Networking.Discovery;

/// <summary>The TCP port this process's own server listens on, or zero for a pure client.</summary>
public sealed class LanHostInfo
{
    private volatile int _listenPort;

    /// <summary>The bound TCP port, or zero when this process does not host a server.</summary>
    public int ListenPort
    {
        get => _listenPort;
        set => _listenPort = value;
    }
}
