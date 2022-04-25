using System.Net;

namespace Swordfish.Library.Networking
{
    public class NetSession
    {
        /// <summary>
        /// The ID of a local session.
        /// </summary>
        public const int Local = 0;

        public IPEndPoint EndPoint { get; set; }

        public int ID { get; set; }

        public override string ToString()
        {
            return $"{ID} @ {EndPoint}";
        }
    }
}
