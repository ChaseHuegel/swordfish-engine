using System;

using Swordfish.Library.Networking.Attributes;
using Swordfish.Library.Networking.Interfaces;

namespace Swordfish.Library.Networking.Packets
{
    [Packet]
    public struct HandshakePacket : ISerializedPacket
    {
        public int ClientID;

        public int ServerID;

        public HandshakePacket(int id)
        {
            ClientID = id;
            ServerID = NetSession.LocalOrUnassigned;
        }

        [PacketHandler(typeof(HandshakePacket))]
        public static void OnHandshakeReceived(NetController net, HandshakePacket packet, NetEventArgs e)
        {
            if (net is Client)
            {
                if (net.Session.ID == NetSession.LocalOrUnassigned && net.TryAddSession(e.EndPoint, packet.ServerID, out NetSession serverSession))
                {
                    net.Session.ID = packet.ClientID;
                    Console.WriteLine($"Joined [{serverSession}] with session [{net.Session}]");
                }
                else
                {
                    //  ? is there a situation where we should accept a new session from the server?
                    //  If we already have a session, do nothing and assume the packet was a mistake on the server's part.
                    Console.WriteLine($"Recieved a session handshake from {e.EndPoint} but already registered [{net.Session}] with [{e.Session}].");
                }
            }
            else
            {
                net.TryAddSession(e.EndPoint, out NetSession newSession);

                Console.WriteLine($"{e.EndPoint} joined, assigning session: [{newSession}]");

                HandshakePacket handshake = new HandshakePacket() {
                    ClientID = newSession.ID,
                    ServerID = net.Session.ID
                };

                net.Send(handshake, e.EndPoint);
            }
        }
    }
}
