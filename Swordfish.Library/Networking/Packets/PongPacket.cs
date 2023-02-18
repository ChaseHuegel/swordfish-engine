using Swordfish.Library.Networking.Attributes;

namespace Swordfish.Library.Networking.Packets
{
    [Packet(RequiresSession = true)]
    public class PongPacket : Packet
    {

    }
}
