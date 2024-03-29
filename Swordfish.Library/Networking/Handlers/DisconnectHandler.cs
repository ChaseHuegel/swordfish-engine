using Swordfish.Library.Diagnostics;
using Swordfish.Library.Networking.Attributes;
using Swordfish.Library.Networking.Packets;

namespace Swordfish.Library.Networking.Handlers
{
    public class DisconnectHandler
    {
        [PacketHandler]
        public static void AgnosticDisconnectHandler(NetController net, DisconnectPacket packet, NetEventArgs e)
        {
            if (!net.TryRemoveSession(e.Session, SessionEndedReason.DISCONNECTED))
                Debugger.Log($"Failed to end session for {e.EndPoint}.", LogType.WARNING);

            if (net is NetClient && !net.IsConnected)
                net.Disconnect();
        }
    }
}
