using Needlefish;

using Swordfish.Library.Networking.Attributes;

namespace Swordfish.Library.Networking.Packets
{
    [Packet(RequiresSession = true, Reliable = true)]
    public class DisconnectPacket : Packet
    {

    }
}
