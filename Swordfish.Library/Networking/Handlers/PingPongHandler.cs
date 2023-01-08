using Swordfish.Library.Diagnostics;
using Swordfish.Library.Networking.Attributes;
using Swordfish.Library.Networking.Packets;

namespace Swordfish.Library.Networking.Handlers
{
    public class PingPongHandler
    {
        [PacketHandler]
        public static void AgnosticPingHandler(NetController net, PingPacket packet, NetEventArgs e)
        {
            Debugger.Log($"Received ping from {e.Session}.");
            net.Send(new PongPacket(), e.Session);
        }

        [PacketHandler]
        public static void AgnosticPongHandler(NetController net, PongPacket packet, NetEventArgs e)
        {
            Debugger.Log($"Received pong from {e.Session}.");
            net.Session.RefreshExpiration();
        }
    }
}
