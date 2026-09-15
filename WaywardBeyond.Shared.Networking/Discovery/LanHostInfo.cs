namespace WaywardBeyond.Shared.Networking.Discovery;

/// <summary>
/// Records the TCP port this process's own hosted server (in host mode) is listening on, so the LAN
/// discovery scanner can exclude the server it hosts by (local source address, advertised TCP port)
/// instead of by address alone. Pure-client processes never set a port, leaving the value at <c>0</c>,
/// which makes the scanner's self-exclusion a no-op.
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