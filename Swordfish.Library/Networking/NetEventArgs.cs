using System;
using System.Net;

namespace Swordfish.Library.Networking
{
    public class NetEventArgs : EventArgs
    {
        public int PacketID;

        public Packet Packet;

        public IPEndPoint EndPoint;

        public NetSession Session;
    }
}
