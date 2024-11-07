using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Swordfish.Library.Networking;

public static class NetUtils
{
    private static ConcurrentDictionary<string, IPAddress> CachedHostAddress { get; } = new();
    private static ConcurrentDictionary<string, IPAddress[]> CachedHostAddresses { get; } = new();

    public static IPAddress GetHostAddress(string hostname)
    {
        return CachedHostAddress.GetOrAdd(hostname, Dns.GetHostAddresses(hostname)[0]);
    }

    public static IPAddress[] GetHostAddresses(string hostname)
    {
        return CachedHostAddresses.GetOrAdd(hostname, Dns.GetHostAddresses(hostname));
    }

    public static bool TryGetHostAddress(string hostname, AddressFamily addressFamily, out IPAddress address)
    {
        address = CachedHostAddress.GetOrAdd(hostname, Dns.GetHostAddresses(hostname).FirstOrDefault(x => x.AddressFamily == addressFamily));
        return address != null;
    }
}