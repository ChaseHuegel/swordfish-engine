using System.Net;
using System;

namespace Swordfish.Library.Networking
{
    public class NetSession
    {
        /// <summary>
        /// The ID of a local or otherwise unassigned session.
        /// </summary>
        public const int LocalOrUnassigned = 0;

        public IPEndPoint EndPoint { get; set; }

        public int ID { get; set; }

        public override string ToString()
        {
            return $"{ID}/{EndPoint}";
        }

        public override bool Equals(object obj)
        {
            NetSession other = obj as NetSession;
            return other != null && this.ID == other.ID && (this?.EndPoint.Equals(other?.EndPoint) ?? false);
        }

        public override int GetHashCode()
        {
            return ID.GetHashCode() ^ EndPoint.GetHashCode();
        }
    }
}
