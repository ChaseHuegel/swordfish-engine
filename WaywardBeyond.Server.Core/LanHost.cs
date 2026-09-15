using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Extensions.Logging;
using Shoal.Modularity;
using Swordfish.ECS;
using WaywardBeyond.Shared.Config;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Discovery;
using WaywardBeyond.Shared.Networking.Serialization;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Server.Core;

/// <summary>
/// Opens the in-process authoritative server to LAN peers over TCP. Runs alongside <see cref="ServerContext"/>
/// in host mode: it listens on <see cref="NetworkingSettings.ServerPort"/>, registers each accepted peer as a
/// client of the shared <see cref="ServerConnectionHub"/> (so sessions, per-client replication and despawn
/// routing all work), and removes a peer from the hub when its socket disconnects. When LAN discovery is
/// enabled, a background thread also broadcasts a <see cref="LanBeacon"/> so LAN clients can auto-detect the
/// server on the discovery port.
/// </summary>
public sealed class LanHost : IEntryPoint, IDisposable
{
    private readonly IEnumerable<INetworkSerializer> _serializers;
    private readonly ServerConnectionHub _hub;
    private readonly NetworkingSettings _settings;
    private readonly LanHostInfo _hostInfo;
    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConcurrentDictionary<TcpTransport, Uuid> _clientIds = new();
    private TcpServerHost? _host;
    private UdpClient? _beacon;
    private volatile bool _beaconRunning;

    public LanHost(
        in IEnumerable<INetworkSerializer> serializers,
        in ServerConnectionHub hub,
        in NetworkingSettings settings,
        in LanHostInfo hostInfo,
        ILoggerFactory loggerFactory
    ) {
        _serializers = serializers;
        _hub = hub;
        _settings = settings;
        _hostInfo = hostInfo;
        _logger = loggerFactory.CreateLogger<LanHost>();
        _loggerFactory = loggerFactory;
    }

    public void Run()
    {
        int port = _settings.ServerPort.Get();
        _host = new TcpServerHost(_serializers, _loggerFactory);
        _host.OnClientAccepted = transport =>
        {
            Uuid clientId = _hub.Add(transport);
            _clientIds[transport] = clientId;
            transport.OnDisconnected = () => RemoveClient(transport);
            _logger.LogInformation("A LAN client connected (id {clientId}).", clientId);
        };

        try
        {
            _host.Listen(port);
            _logger.LogInformation("Listening for LAN connections on port {port}.", _host.LocalPort);
            _hostInfo.ListenPort = _host.LocalPort;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start the LAN listener on port {port}.", port);
        }

        StartBeacon();
    }

    /// <summary>
    /// Starts the UDP discovery beacon on its own background thread. The broadcast socket is bound after the
    /// TCP listener so <see cref="_host.LocalPort"/> is known. On socket failure the loop is killed outright —
    /// LAN discovery is disabled rather than retried.
    /// </summary>
    private void StartBeacon()
    {
        if (!_settings.LanDiscovery.Get())
        {
            return;
        }

        if (_host!.LocalPort == 0)
        {
            _logger.LogWarning("LAN listener did not bind; LAN discovery disabled.");
            return;
        }

        try
        {
            _beacon = new UdpClient { EnableBroadcast = true };
            _beaconRunning = true;
            int intervalMs = Math.Max(100, _settings.DiscoveryBroadcastSeconds.Get() * 1000);
            int tcpPort = _host!.LocalPort;
            var thread = new Thread(() => BroadcastBeacons(tcpPort, intervalMs))
            {
                IsBackground = true,
                Name = "LAN BEACON",
            };
            thread.Start();
            _logger.LogInformation("LAN discovery beacon started on port {port}.", _settings.DiscoveryPort.Get());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start the LAN discovery beacon; LAN discovery disabled.");
            StopBeacon();
        }
    }

    private void BroadcastBeacons(int tcpPort, int intervalMs)
    {
        int discoveryPort = _settings.DiscoveryPort.Get();
        string serverName = _settings.ServerName.Get();

        while (_beaconRunning)
        {
            byte[] payload = new LanBeacon
            {
                ServerName = serverName,
                TcpPort = tcpPort,
                ProtocolVersion = LanDiscovery.ProtocolVersion,
                PlayerCount = _hub.Count,
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

    private void RemoveClient(TcpTransport transport)
    {
        if (_clientIds.TryRemove(transport, out Uuid clientId))
        {
            _hub.Remove(clientId);
        }
    }

    public void Dispose()
    {
        StopBeacon();
        _host?.Dispose();
    }
}