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
            public static BeginPacket New => new BeginPacket
            {
                Signature = ValidationSignature
            };

            public string Signature;
        }

        [Packet(RequiresSession = false, Reliable = true)]
        public class AcceptPacket : Packet
        {
            public static AcceptPacket New => new AcceptPacket
            {
                Signature = ValidationSignature
            };

            public int AcceptedSessionID;

            public int RemoteSessionID;

            public string Signature;
        }

        public static string ValidationSignature { get; set; }

        public static Func<EndPoint, bool> ValidateCallback { get; set; }

        [PacketHandler]
        public static void HandshakeBeginHandler(NetController net, BeginPacket packet, NetEventArgs e)
        {
            //  Validate the handshake.
            //  Use the callback if available, and confirm signatures match
            if (packet.Signature == ValidationSignature && (ValidateCallback?.Invoke(e.EndPoint) ?? true))
            {
                if (!net.TryAddSession(e.EndPoint, out NetSession newSession))
                    return;

                Debugger.Log($"{e.EndPoint} joined, assigning session: [{newSession}]");

                AcceptPacket accept = new AcceptPacket()
                {
                    AcceptedSessionID = newSession.ID,
                    RemoteSessionID = net.Session.ID,
                    Signature = ValidationSignature
                };

                net.Send(accept, e.EndPoint);
            }
            else
            {
                Debugger.Log($"{e.EndPoint} tried to join, failed to validate handshake.", LogType.ERROR);
            }
        }

        [PacketHandler]
        public static void HandshakeAcceptHandler(NetController net, AcceptPacket packet, NetEventArgs e)
        {
            if (net.Session.ID == NetSession.LocalOrUnassigned && net.TryAddSession(e.EndPoint, packet.RemoteSessionID, out NetSession serverSession))
            {
                Debugger.Log($"Joined [{serverSession}] with session [{net.Session}]");
                net.Session.ID = packet.AcceptedSessionID;
                net.Connected?.Invoke(net, NetEventArgs.Empty);
            }
            else
            {
                //  ? is there a situation where we should accept a new session?
                //  If we already have a session, do nothing and assume the packet was a fluke.
                Debugger.Log($"Recieved a session handshake from {e.EndPoint} but already have an active session with a host.", LogType.WARNING);
            }
        }
    }
}
