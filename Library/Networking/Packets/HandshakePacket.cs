using System;

using Swordfish.Library.Networking.Attributes;
using Swordfish.Library.Networking.Interfaces;

namespace Swordfish.Library.Networking.Packets
{
    [Packet]
    public struct HandshakePacket : ISerializedPacket
    {
        public int SessionID;

        public HandshakePacket(int id)
        {
            SessionID = id;
        }
        
        [PacketHandler(typeof(HandshakePacket))]
        public static void OnHandshakeReceived(NetController net, HandshakePacket packet, NetEventArgs e)
        {
            if (net is Client)
            {
                if (net.Session.ID == NetSession.Local)
                {
                    net.Session.ID = packet.SessionID;
                    Console.WriteLine($"Joined with session [{net.Session}]");
                }
                else
                {
                    //  ? is there a situation where we should accept a new session from the server?
                    //  If we already have a session, do nothing and assume the packet was a mistake on the server's part.
                    Console.WriteLine($"Recieved a session handshake but already registered session [{net.Session}]?");
                }
            }
            else
            {
                NetSession newSession = net.AddSession(e.EndPoint);

                Console.WriteLine($"{e.EndPoint} joined, assigning session [{newSession}]");
                net.Send(new HandshakePacket(newSession.ID), e.EndPoint);
            }
        }
    }
}
