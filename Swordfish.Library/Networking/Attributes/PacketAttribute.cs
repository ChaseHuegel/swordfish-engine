using System;

namespace Swordfish.Library.Networking.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class PacketAttribute : Attribute
    {
        public bool RequiresSession = true;

        public bool Ordered = false;

        public int? PacketID;
    }
}
