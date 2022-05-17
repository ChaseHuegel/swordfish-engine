using System;

namespace Swordfish.Library.Networking.Attributes
{
    /// <summary>
    /// Decorates a method to process a received packet.
    /// <example>
    /// Example signature:
    /// <code>public static void OnPacketReceived(NetController net, IPacket packet, NetEventArgs e)</code>
    /// </example>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class PacketHandlerAttribute : Attribute
    {
        public Type PacketType;

        public PacketHandlerAttribute() { }

        public PacketHandlerAttribute(Type packetType)
        {
            PacketType = packetType;
        }
    }
}
