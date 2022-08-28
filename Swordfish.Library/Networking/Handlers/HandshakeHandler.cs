using System;
using System.Net;

using Swordfish.Library.Diagnostics;
using Swordfish.Library.Networking.Attributes;
using Swordfish.Library.Networking.Packets;

namespace Swordfish.Library.Networking.Handlers
{
    public class HandshakeHandler
    {
        public static Func<EndPoint, bool> ValidateHandshakeCallback { get; set; }

        [ClientPacketHandler]
        public static void ClientHandshakeHandler(NetController net, HandshakePacket packet, NetEventArgs e)
        {
            if (net.Session.ID == NetSession.LocalOrUnassigned && net.TryAddSession(e.EndPoint, packet.ServerID, out NetSession serverSession))
            {
                net.Session.ID = packet.ClientID;
                Debugger.Log($"Joined [{serverSession}] with session [{net.Session}]");
                net.Connected?.Invoke(net, NetEventArgs.Empty);
            }
            else
            {
                //  ? is there a situation where we should accept a new session from the server?
                //  If we already have a session, do nothing and assume the packet was a mistake on the server's part.
                Debugger.Log($"Recieved a session handshake from {e.EndPoint} but already registered [{net.Session}] with [{e.Session}].", LogType.WARNING);
            }
        }

        [ServerPacketHandler]
        public static void ServerHandshakeHandler(NetController net, HandshakePacket packet, NetEventArgs e)
        {
            //  Validate the handshake.
            //  Use the callback if available, and confirm signatures match
            if (packet.Signature == HandshakePacket.ValidationSignature && (ValidateHandshakeCallback?.Invoke(e.EndPoint) ?? true))
            {
                net.TryAddSession(e.EndPoint, out NetSession newSession);

                Debugger.Log($"{e.EndPoint} joined, assigning session: [{newSession}]");

                HandshakePacket handshake = new HandshakePacket()
                {
                    ClientID = newSession.ID,
                    ServerID = net.Session.ID,
                    Signature = HandshakePacket.ValidationSignature
                };

                net.Send(handshake, e.EndPoint);
            }
            else
            {
                Debugger.Log($"{e.EndPoint} tried to join, failed to validate handshake.", LogType.ERROR);
            }
        }
    }
}
