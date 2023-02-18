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

        public List<PacketHandler> Handlers { get; set; } = new List<PacketHandler>();

        public bool RequiresSession { get; set; }

        public bool Ordered { get; set; }

        public bool Reliable { get; set; }

        public override string ToString()
        {
            return $"{Type} [id: {ID}] [requires session: {RequiresSession}] [ordered: {Ordered}] [reliable: {Reliable}]";
        }
    }
}
