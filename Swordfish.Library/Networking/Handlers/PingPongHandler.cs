using Swordfish.Library.Diagnostics;
using Swordfish.Library.Networking.Attributes;
using Swordfish.Library.Networking.Packets;

namespace Swordfish.Library.Networking.Handlers
{
    public class PingPongHandler
    {
        [PacketHandler]
        public static void AgnosticPingHandler(NetController net, DisconnectPacket packet, NetEventArgs e)
        {
            Debugger.Log($"Received ping from {e.Session}.");
            net.Send(new PongPacket(), e.Session);
        }

        [PacketHandler]
        public static void AgnosticPongHandler(NetController net, DisconnectPacket packet, NetEventArgs e)
        {
            Debugger.Log($"Received pong from {e.Session}.");
            net.Session.RefreshExpiration();
        }
    }
}
