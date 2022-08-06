using System;
using Needlefish;

namespace Swordfish.Library.Networking
{
    public class Packet : IDataBody
    {
        public int SessionID;
        public int PacketID;
        public uint Sequence;
    }
}
