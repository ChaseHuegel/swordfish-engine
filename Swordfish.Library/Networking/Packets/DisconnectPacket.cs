using Swordfish.Library.Diagnostics;
using Swordfish.Library.Networking.Attributes;
using Swordfish.Library.Networking.Interfaces;

namespace Swordfish.Library.Networking.Packets
{
    [Packet(RequiresSession = true)]
    public struct DisconnectPacket : ISerializedPacket
    {
        [PacketHandler]
        public static void DisconnectHandler(NetController net, DisconnectPacket packet, NetEventArgs e)
        {            
            if (!net.TryRemoveSession(e.Session))
                Debug.Log($"Failed to end session for {e.EndPoint}.", LogType.WARNING);
            
            if (net is NetClient && !net.IsConnected)
                net.InvokeLocalDisconnect();
        }
    }
}
