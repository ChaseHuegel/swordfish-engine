using Swordfish.Networking;

namespace Swordfish.Tests;

public class PacketFilter<TEndPoint> : IFilter<PacketReceivedArgs<TEndPoint>>
{
    public bool Check(PacketReceivedArgs<TEndPoint> target)
    {
        return true;
    }
}