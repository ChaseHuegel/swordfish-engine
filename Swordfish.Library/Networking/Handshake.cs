using Swordfish.Library.Networking.Attributes;

namespace Swordfish.Library.Networking;

public static class Handshake
{
    [Packet(RequiresSession = false, Reliable = true)]
    public class BeginPacket : Packet
    {
        public static BeginPacket New(string secret) => new()
        {
            Secret = secret,
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
            {
                return;
            }

            var accept = new AcceptPacket()
            {
                AcceptedSessionID = newSession.ID,
                RemoteSessionID = net.Session.ID,
                Secret = packet.Secret,
            };

            net.Send(accept, e.EndPoint);
        }
    }

    [PacketHandler]
    public static void HandshakeAcceptHandler(NetController net, AcceptPacket packet, NetEventArgs e)
    {
        if (net.IsConnected || net.Session.ID != NetSession.LOCAL_OR_UNASSIGNED)
        {
            return;
        }

        if (!net.TryAddSession(e.EndPoint, packet.RemoteSessionID, out NetSession serverSession))
        {
            return;
        }

        if (!net.HandshakeAcceptCallback?.Invoke(e.EndPoint, packet.Secret) ?? true)
        {
            return;
        }

        net.Session.ID = packet.AcceptedSessionID;
        net.Connected?.Invoke(net, e);
    }
}