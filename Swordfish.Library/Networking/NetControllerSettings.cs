using System;
using System.Net;

namespace Swordfish.Library.Networking
{
    public struct NetControllerSettings
    {
        public const int DefaultTickRate = 10;

        public IPAddress Address;
        public int Port;
        public Host DefaultHost;
        public TimeSpan SessionExpiration;
        public TimeSpan KeepAlive;
        public int TickRate;

        public NetControllerSettings(
            IPAddress address,
            int port,
            Host defaultHost,
            TimeSpan sessionExpiration,
            TimeSpan keepAlive,
            int tickRate)
        {
            Address = address;
            Port = port;
            DefaultHost = defaultHost;
            SessionExpiration = sessionExpiration;
            KeepAlive = keepAlive;
            TickRate = tickRate;
        }

        public NetControllerSettings(Host defaultHost) : this(default, default, defaultHost, default, default, default) { }

        public NetControllerSettings(int port) : this(default, port, default, default, default, default) { }

        public NetControllerSettings(IPAddress address, int port) : this(address, port, default, default, default, default) { }
    }
}
