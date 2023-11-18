using Swordfish.Networking;

namespace Swordfish.Tests;

public class PacketFilter : IFilter<PacketHeader>
{
    public bool Check(PacketHeader target)
    {
        return true;
    }
}