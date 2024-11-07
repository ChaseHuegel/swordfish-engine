using Swordfish.Library.Networking.Attributes;

namespace Swordfish.Library.Networking.Packets
{
    [Packet(RequiresSession = false)]
    public class AckPacket : Packet
    {
        public static AckPacket New(int ackPacketID, uint ackSequence) => new AckPacket
        {
            AckPacketID = ackPacketID,
            AckSequence = ackSequence
        };

        public int AckPacketID;

        public uint AckSequence;
    }
}
