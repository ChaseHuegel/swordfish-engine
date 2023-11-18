using System;

namespace Swordfish.Library.Networking.Attributes
{
    /// <remarks>
    /// Only handles packets received by a <see cref="NetServer"/>.
    /// </remarks>
    /// <inheritdoc/>
    [AttributeUsage(AttributeTargets.Method)]
    public class ServerPacketHandlerAttribute : PacketHandlerAttribute
    {
        public ServerPacketHandlerAttribute() { }

        public ServerPacketHandlerAttribute(Type packetType)
        {
            PacketType = packetType;
        }
    }
}
