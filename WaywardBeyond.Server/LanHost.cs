using System;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Extensions.Logging;
using Shoal.Modularity;
using WaywardBeyond.Config;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Discovery;

namespace WaywardBeyond.Server;

/// <summary>
/// Opens the authoritative server to LAN peers. Calls <see cref="ServerRpcHost.StartLan"/> on the
/// configured port and, when discovery is enabled, broadcasts a <see cref="LanBeacon"/> so LAN clients
/// can auto-detect the server on the discovery port.
/// </summary>
public sealed class LanHost : IEntryPoint, IDisposable
{
    private readonly ServerRpcHost _rpcHost;
    private readonly ServerWorldHost _worldHost;
    private readonly NetworkingConfig _config;
    private readonly LanHostInfo _hostInfo;
    private readonly ILogger<LanHost> _logger;
    private UdpClient? _beacon;
    private volatile bool _beaconRunning;

    public LanHost(
        ServerRpcHost rpcHost,
        ServerWorldHost worldHost,
        NetworkingConfig config,
        LanHostInfo hostInfo,
        ILoggerFactory loggerFactory
    ) {
        _rpcHost = rpcHost;
        _worldHost = worldHost;
        _config = config;
        _hostInfo = hostInfo;
        _logger = loggerFactory.CreateLogger<LanHost>();
    }

    public void Run()
    {
        int port = _config.Server.Port.Get();
        try
        {
            _rpcHost.StartLan(port);
            _hostInfo.ListenPort = _rpcHost.LanPort;
            _logger.LogInformation("Listening for LAN connections on port {port}.", _rpcHost.LanPort);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start the LAN listener on port {port}.", port);
            return;
        }

        StartBeacon();
    }

    /// <summary>
    /// Starts the UDP discovery beacon on its own background thread. The broadcast socket is bound after the
    /// RPC listener so the bound port is known. On socket failure the loop is killed outright - LAN
    /// discovery is disabled rather than retried.
    /// </summary>
    private void StartBeacon()
    {
        if (!_config.Discovery.Enabled.Get())
        {
            return;
        }

        if (_rpcHost.LanPort == 0)
        {
            _logger.LogWarning("LAN listener did not bind; LAN discovery disabled.");
            return;
        }

        try
        {
            _beacon = new UdpClient { EnableBroadcast = true };
            _beaconRunning = true;
            int intervalMs = Math.Max(100, _config.Discovery.BroadcastSeconds.Get() * 1000);
            int tcpPort = _rpcHost.LanPort;
            var thread = new Thread(() => BroadcastBeacons(tcpPort, intervalMs))
            {
                IsBackground = true,
                Name = "LAN BEACON",
            };
            thread.Start();
            _logger.LogInformation("LAN discovery beacon started on port {port}.", _config.Discovery.Port.Get());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start the LAN discovery beacon; LAN discovery disabled.");
            StopBeacon();
        }
    }

    private void BroadcastBeacons(int tcpPort, int intervalMs)
    {
        int discoveryPort = _config.Discovery.Port.Get();
        string serverName = _config.Server.Name.Get();

        while (_beaconRunning)
        {
            byte[] payload = new LanBeacon
            {
                ServerName = serverName,
                TcpPort = tcpPort,
                ProtocolVersion = LanDiscovery.ProtocolVersion,
                PlayerCount = _worldHost.PlayerCount,
            }.Serialize();

            try
            {
                _beacon!.Send(payload, payload.Length, LanDiscovery.BroadcastAddress, discoveryPort);
            }
            catch (SocketException)
            {
                _logger.LogWarning("LAN beacon broadcast failed; LAN discovery disabled.");
                break;
            }
            catch (ObjectDisposedException)
            {
                break; //  Beacon disposed during shutdown.
            }

            for (int slept = 0; slept < intervalMs && _beaconRunning; slept += 100)
            {
                Thread.Sleep(Math.Min(100, intervalMs - slept));
            }
        }

        _beaconRunning = false;
    }

    private void StopBeacon()
    {
        _beaconRunning = false;
        _beacon?.Dispose();
    }

    public void Dispose()
    {
        StopBeacon();
    }
}
