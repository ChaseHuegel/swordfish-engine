using Needlefish;

using Swordfish.Library.Networking.Attributes;

namespace Swordfish.Library.Networking.Packets
{
    [Packet(RequiresSession = false)]
    public class AckPacket : Packet
    {
        public static AckPacket New(int ackPacketID, uint ackSequence) => new AckPacket(ackPacketID, ackSequence);

        public int AckPacketID;

        public uint AckSequence;

        public AckPacket(int ackPacketID, uint ackSequence)
        {
            AckPacketID = ackPacketID;
            AckSequence = ackSequence;
        }
    }
}
