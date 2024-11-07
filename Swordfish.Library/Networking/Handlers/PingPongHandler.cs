using Swordfish.Library.Networking.Attributes;
using Swordfish.Library.Networking.Packets;

namespace Swordfish.Library.Networking.Handlers
{
    public class PingPongHandler
    {
        [PacketHandler]
        public static void AgnosticPingHandler(NetController net, PingPacket packet, NetEventArgs e)
        {
            net.Send(new PongPacket(), e.Session);
        }

        [PacketHandler]
        public static void AgnosticPongHandler(NetController net, PongPacket packet, NetEventArgs e)
        {
            net.Session.RefreshExpiration();
        }
    }
}
