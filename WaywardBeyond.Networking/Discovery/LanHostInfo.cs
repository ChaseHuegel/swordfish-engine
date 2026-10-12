namespace WaywardBeyond.Networking.Discovery;

/// <summary>
/// The TCP port this process's own hosted server listens on, so the LAN scanner can exclude it;
/// pure-client processes leave the value at <see langword="0"/>.
/// </summary>
public sealed class LanHostInfo
{
    private volatile int _listenPort;

    public int ListenPort
    {
        get => _listenPort;
        set => _listenPort = value;
    }
}