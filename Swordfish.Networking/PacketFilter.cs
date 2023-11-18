using Swordfish.Networking;

namespace Swordfish.Tests;

public class PacketFilter : IFilter<Packet>
{
    public bool Check(Packet target)
    {
        return true;
    }
}