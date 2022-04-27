using System.Reflection;

using Swordfish.Library.Networking.Attributes;

namespace Swordfish.Library.Networking
{
    public class PacketHandler
    {
        public MethodInfo Method { get; set; }

        public PacketHandlerType Type { get; set; }

        public PacketHandler(MethodInfo method, PacketHandlerAttribute attribute)
        {
            Method = method;

            switch (attribute)
            {
                case ClientPacketHandlerAttribute _:
                    Type = PacketHandlerType.CLIENT;
                    break;
                case ServerPacketHandlerAttribute _:
                    Type = PacketHandlerType.SERVER;
                    break;
                default:
                    Type = PacketHandlerType.AGNOSTIC;
                    break;
            }
        }

        public PacketHandler(MethodInfo method, PacketHandlerType type)
        {
            Method = method;
            Type = type;
        }
    }
}
