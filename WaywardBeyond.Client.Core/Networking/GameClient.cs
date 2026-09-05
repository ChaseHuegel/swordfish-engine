using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Client.Core.Networking;

public sealed class GameClient
{
    public IClientConnection Transport { get; }

    public bool IsConnected => Transport.IsConnected;
    public bool IsLocal => Transport.IsLocal;

    public GameClient(in IClientConnection transport)
    {
        Transport = transport;
    }
}
