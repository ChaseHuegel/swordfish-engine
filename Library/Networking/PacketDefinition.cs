using System;
using System.Collections.Generic;
using System.Reflection;
using Swordfish.Library.Extensions;

namespace Swordfish.Library.Networking
{
    public class PacketDefinition
    {
        public int ID { get; set; }

        public Type Type { get; set; }

        public List<MethodInfo> Handlers { get; set; } = new List<MethodInfo>();

        public bool RequiresSession { get; set; } = true;

        public override string ToString()
        {
            return $"{Type.ToString().TruncateStartUpTo(24).Prepend("..")} [id: {ID}] [requires session: {RequiresSession}]";
        }
    }
}
