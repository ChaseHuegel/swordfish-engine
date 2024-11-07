using System;
using System.Net;
using System.Net.Sockets;

namespace Swordfish.Library.Networking;

public struct NetControllerSettings
{
    public const int DEFAULT_TICK_RATE = 10;
    public const int DEFAULT_MAX_SESSIONS = 20;

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
        int tickRate = DEFAULT_TICK_RATE,
        int maxSessions = DEFAULT_MAX_SESSIONS)
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

    public NetControllerSettings(Host defaultHost) : this(AddressFamily.Unspecified, default, default, defaultHost, default, default, DEFAULT_TICK_RATE, DEFAULT_MAX_SESSIONS) { }

    public NetControllerSettings(AddressFamily addressFamily) : this(addressFamily, default, default, default, default, default, DEFAULT_TICK_RATE, DEFAULT_MAX_SESSIONS) { }

    public NetControllerSettings(int port) : this(AddressFamily.Unspecified, default, port, default, default, default, DEFAULT_TICK_RATE, DEFAULT_MAX_SESSIONS) { }

    public NetControllerSettings(int port, AddressFamily addressFamily) : this(addressFamily, default, port, default, default, default, DEFAULT_TICK_RATE, DEFAULT_MAX_SESSIONS) { }

    public NetControllerSettings(IPAddress address, int port) : this(AddressFamily.Unspecified, address, port, default, default, default, DEFAULT_TICK_RATE, DEFAULT_MAX_SESSIONS) { }
}