using System;

namespace Swordfish.Library.Networking.Attributes
{
    [AttributeUsage(AttributeTargets.Struct)]
    public class PacketAttribute : Attribute
    {
        public bool RequiresSession = true;

        public int? PacketID;
    }
}
