using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using WaywardBeyond.Shared.Config;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Discovery;

namespace WaywardBeyond.Client.Core.Networking;

/// <summary>A server advertized by a <see cref="LanBeacon"/> on the LAN discovery port.</summary>
internal readonly record struct DiscoveredServer(string Name, string Host, int Port, int Players);

/// <summary>
/// Client half of LAN discovery. Listens on the discovery port for a bounded window, streaming each newly
/// discovered server as its beacon arrives so callers render results incrementally rather than waiting for
/// the whole window. Drops mismatched protocol versions, dedupes by endpoint, and runs off the UI thread
/// (each awaited receive uses <c>ConfigureAwait(false)</c>). When this process is itself a host, it ignores
/// beacons it received from its own hosted server — identified by a local source address and the advertised
/// TCP port it is listening on — so that server is not offered as a join option while other hosts on the
/// same machine remain discoverable.
/// </summary>
internal sealed class LanDiscoveryService
{
    private static readonly HashSet<IPAddress> _localAddresses = GetLocalAddresses();
    private readonly NetworkingSettings _settings;
    private readonly LanHostInfo _hostInfo;
    private readonly bool _isHost;

    public LanDiscoveryService(in NetworkingSettings settings, in LanHostInfo hostInfo)
    {
        _settings = settings;
        _hostInfo = hostInfo;
        _isHost = NetworkModeResolver.Resolve() == NetworkMode.Host;
    }

    /// <summary>
    /// Streams every unique server discovered within the scan window. The window is bounded by
    /// <see cref="NetworkingSettings.DiscoveryScanSeconds"/>; the enumeration completes normally when it
    /// elapses or when <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    public async IAsyncEnumerable<DiscoveredServer> ScanAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        int windowMs = Math.Max(100, _settings.DiscoveryScanSeconds.Get() * 1000);
        int discoveryPort = _settings.DiscoveryPort.Get();

        UdpClient udp;
        try
        {
            udp = new UdpClient(discoveryPort);
        }
        catch (SocketException)
        {
            yield break; //  Could not bind the discovery socket.
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(windowMs);
        CancellationToken token = cts.Token;
        var seen = new ConcurrentDictionary<string, DiscoveredServer>();

        using (udp)
        {
            while (!token.IsCancellationRequested)
            {
                UdpReceiveResult datagram;
                try
                {
                    datagram = await udp.ReceiveAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break; //  Scan window elapsed.
                }
                catch (SocketException)
                {
                    break; //  Discovery socket failed.
                }

                DiscoveredServer? server = TryDecode(datagram);
                if (server != null && seen.TryAdd($"{server.Value.Host}:{server.Value.Port}", server.Value))
                {
                    yield return server.Value;
                }
            }
        }
    }

    private DiscoveredServer? TryDecode(in UdpReceiveResult datagram)
    {
        try
        {
            LanBeacon beacon = LanBeacon.Deserialize(datagram.Buffer);
            if (beacon.ProtocolVersion != LanDiscovery.ProtocolVersion || beacon.TcpPort < 1 || beacon.TcpPort > 65535)
            {
                return null;
            }

            if (_isHost && IsLocalAddress(datagram.RemoteEndPoint.Address) && beacon.TcpPort == _hostInfo.ListenPort)
            {
                return null; //  Own hosted server on this machine.
            }

            string host = datagram.RemoteEndPoint.Address.ToString();
            return new DiscoveredServer(beacon.ServerName ?? string.Empty, host, beacon.TcpPort, beacon.PlayerCount);
        }
        catch (Exception)
        {
            return null; //  Malformed or foreign datagram on the discovery port.
        }
    }

    private static HashSet<IPAddress> GetLocalAddresses()
    {
        var addresses = new HashSet<IPAddress> { IPAddress.Loopback, IPAddress.IPv6Loopback };
        try
        {
            foreach (IPAddress address in Dns.GetHostAddresses(Dns.GetHostName()))
            {
                addresses.Add(address);
            }
        }
        catch (SocketException)
        {
            //  Hostname resolution unavailable. Fall back to loopback only.
        }

        return addresses;
    }

    private static bool IsLocalAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        return _localAddresses.Contains(address);
    }
}