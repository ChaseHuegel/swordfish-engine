using System;
using System.Net;

using Swordfish.Library.Diagnostics;
using Swordfish.Library.Networking.Attributes;
using Swordfish.Library.Networking.Interfaces;

namespace Swordfish.Library.Networking.Packets
{
    [Packet(RequiresSession = false)]
    public struct HandshakePacket : ISerializedPacket
    {
        public static string ValidationSignature { get; set; }

        public static HandshakePacket New => new HandshakePacket {
            Signature = ValidationSignature
        };
        
        public int ClientID;

        public int ServerID;

        public string Signature;
    }
}
