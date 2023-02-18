using Needlefish;

using Swordfish.Library.Networking.Attributes;

namespace Swordfish.Library.Networking.Packets
{
    [Packet(RequiresSession = false, Reliable = true)]
    public class HandshakePacket : Packet
    {
        public static string ValidationSignature { get; set; }

        public static HandshakePacket New => new HandshakePacket
        {
            Signature = ValidationSignature
        };

        public int ClientID;

        public int ServerID;

        public string Signature;
    }
}
