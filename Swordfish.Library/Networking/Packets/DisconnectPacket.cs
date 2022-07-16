using Needlefish;

using Swordfish.Library.Networking.Attributes;

namespace Swordfish.Library.Networking.Packets
{
    [Packet(RequiresSession = true)]
    public struct DisconnectPacket : IDataBody
    {
        
    }
}
