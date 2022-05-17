using System;

namespace Swordfish.Library.Networking.Attributes
{
    /// <remarks>
    /// Only handles packets received by a <see cref="Swordfish.Library.Networking.NetClient"/>.
    /// </remarks>
    /// <inheritdoc/>
    [AttributeUsage(AttributeTargets.Method)]
    public class ClientPacketHandlerAttribute : PacketHandlerAttribute
    {
    }
}
