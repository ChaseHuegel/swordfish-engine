using System.Net;

namespace Swordfish.Networking;

public struct NetEventArgs
{
    public static NetEventArgs Empty => new();

    public Packet Packet;

    public IPEndPoint EndPoint;

    // public NetSession Session;
}
