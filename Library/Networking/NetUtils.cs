using System;
using System.Collections.Concurrent;
using System.Net;

namespace Swordfish.Library.Networking
{
    public static class NetUtils
    {
        private static ConcurrentDictionary<string, IPAddress> CachedHostAddresses { get; } = new ConcurrentDictionary<string, IPAddress>();

        public static IPAddress GetHostAddress(string hostname)
        {
            return CachedHostAddresses.GetOrAdd(hostname, Dns.GetHostAddresses(hostname)[0]);
        }
    }
}
