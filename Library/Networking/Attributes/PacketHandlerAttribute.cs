using System;

namespace Swordfish.Library.Networking.Attributes
{
    /// <summary>
    /// Decorates a method to process a received packet.
    /// <para/>
    /// The method must be public static and accept parameters for <see cref="Swordfish.Library.Networking.NetController"/>, <see cref="Swordfish.Library.Networking.Interfaces.ISerializedPacket"/>, and <see cref="Swordfish.Library.Networking.NetEventArgs"/>
    /// <para/>
    /// Example:
    /// public static void OnPacketReceived(NetController net, IPacket packet, NetEventArgs e)
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class PacketHandlerAttribute : Attribute
    {
        public Type PacketType;

        public PacketHandlerAttribute(Type packetType)
        {
            PacketType = packetType;
        }
    }
}
