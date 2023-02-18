using System;
using System.Net;
using System.Net.Sockets;

namespace Swordfish.Library.Networking
{
    public struct NetControllerSettings
    {
        public const int DefaultTickRate = 10;
        public const int DefaultMaxSessions = 20;

        public AddressFamily AddressFamily;
        public IPAddress Address;
        public int Port;
        public Host DefaultHost;
        public TimeSpan SessionExpiration;
        public TimeSpan KeepAlive;
        public int TickRate;
        public int MaxSessions;

        public NetControllerSettings(
            AddressFamily addressFamily,
            IPAddress address,
            int port,
            Host defaultHost,
            TimeSpan sessionExpiration,
            TimeSpan keepAlive,
            int tickRate = DefaultTickRate,
            int maxSessions = DefaultMaxSessions)
        {
            AddressFamily = addressFamily;
            Address = address;
            Port = port;
            DefaultHost = defaultHost;
            SessionExpiration = sessionExpiration;
            KeepAlive = keepAlive;
            TickRate = tickRate;
            MaxSessions = maxSessions;
        }

        public NetControllerSettings(Host defaultHost) : this(AddressFamily.Unspecified, default, default, defaultHost, default, default, DefaultTickRate, DefaultMaxSessions) { }

        public NetControllerSettings(AddressFamily addressFamily) : this(addressFamily, default, default, default, default, default, DefaultTickRate, DefaultMaxSessions) { }

        public NetControllerSettings(int port) : this(AddressFamily.Unspecified, default, port, default, default, default, DefaultTickRate, DefaultMaxSessions) { }

        public NetControllerSettings(int port, AddressFamily addressFamily) : this(addressFamily, default, port, default, default, default, DefaultTickRate, DefaultMaxSessions) { }

        public NetControllerSettings(IPAddress address, int port) : this(AddressFamily.Unspecified, address, port, default, default, default, DefaultTickRate, DefaultMaxSessions) { }
    }
}
