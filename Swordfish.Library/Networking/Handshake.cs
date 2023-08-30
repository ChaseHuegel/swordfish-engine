using System;
using System.Net;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Networking.Attributes;

namespace Swordfish.Library.Networking
{
    public static class Handshake
    {
        [Packet(RequiresSession = false, Reliable = true)]
        public class BeginPacket : Packet
        {
            public static BeginPacket New(string secret) => new BeginPacket
            {
                Secret = secret
            };

            public string Secret;
        }

        [Packet(RequiresSession = false, Reliable = true)]
        public class AcceptPacket : Packet
        {
            public int AcceptedSessionID;

            public int RemoteSessionID;

            public string Secret;
        }

        [PacketHandler]
        public static void HandshakeBeginHandler(NetController net, BeginPacket packet, NetEventArgs e)
        {
            //  Validate the handshake
            if (net.HandshakeValidateCallback?.Invoke(e.EndPoint, packet.Secret) ?? true)
            {
                if (!net.TryAddSession(e.EndPoint, out NetSession newSession))
                    return;

                Debugger.Log($"{e.EndPoint} joined, assigning session: [{newSession}]");

                AcceptPacket accept = new AcceptPacket()
                {
                    AcceptedSessionID = newSession.ID,
                    RemoteSessionID = net.Session.ID,
                    Secret = packet.Secret
                };

                net.Send(accept, e.EndPoint);
            }
            else
            {
                Debugger.Log($"{e.EndPoint} tried to join, failed to validate handshake.", LogType.WARNING);
            }
        }

        [PacketHandler]
        public static void HandshakeAcceptHandler(NetController net, AcceptPacket packet, NetEventArgs e)
        {
            if (net.IsConnected || net.Session.ID != NetSession.LocalOrUnassigned)
            {
                //  ? is there a situation where we should accept a new session?
                Debugger.Log($"Recieved a session handshake from {e.EndPoint} but already have an active session with a host.", LogType.WARNING);
                return;
            }

            if (!net.TryAddSession(e.EndPoint, packet.RemoteSessionID, out NetSession serverSession))
            {
                Debugger.Log($"Recieved a session handshake from {e.EndPoint} but failed to establish a session.", LogType.WARNING);
                return;
            }

            if (!net.HandshakeAcceptCallback?.Invoke(e.EndPoint, packet.Secret) ?? true)
            {
                Debugger.Log($"Recieved a session handshake from {e.EndPoint} but failed to validate secret.", LogType.WARNING);
                return;
            }

            net.Session.ID = packet.AcceptedSessionID;
            Debugger.Log($"Joined [{serverSession}] with session [{net.Session}]");
            net.Connected?.Invoke(net, e);
        }
    }
}
