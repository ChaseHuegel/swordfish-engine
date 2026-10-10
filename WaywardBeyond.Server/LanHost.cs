using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Extensions.Logging;
using Shoal.Modularity;
using WaywardBeyond.Config;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Discovery;
using WaywardBeyond.Networking.Serialization;
using WaywardBeyond.Networking.Transport;
using WaywardBeyond.Server.Permissions;

namespace WaywardBeyond.Server;

/// <summary>
/// Opens the in-process authoritative server to LAN peers over TCP. Runs alongside <see cref="ServerWorldHost"/>
/// in host mode: it listens on <see cref="NetworkingConfig.ServerPort"/>, routes each accepted peer
/// into the pending-join set (the host binds it to its world's hub when its <c>JoinRequest</c> arrives),
/// and on socket disconnect releases the peer from the world host. When LAN discovery is enabled, a
/// background thread also broadcasts a <see cref="LanBeacon"/> so LAN clients can auto-detect the server
/// on the discovery port.
/// </summary>
public sealed class LanHost : IEntryPoint, IDisposable
{
    private readonly IEnumerable<INetworkSerializer> _serializers;
    private readonly PendingJoins _pendingJoins;
    private readonly ServerWorldHost _worldHost;
    private readonly NetworkingConfig _config;
    private readonly LanHostInfo _hostInfo;
    private readonly ConnectionClaims _claims;
    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConcurrentDictionary<TcpTransport, byte> _clientIds = new();
    private TcpServerHost? _host;
    private UdpClient? _beacon;
    private volatile bool _beaconRunning;

    public LanHost(
        in IEnumerable<INetworkSerializer> serializers,
        in PendingJoins pendingJoins,
        in ServerWorldHost worldHost,
        in NetworkingConfig config,
        in LanHostInfo hostInfo,
        in ConnectionClaims claims,
        ILoggerFactory loggerFactory
    ) {
        _serializers = serializers;
        _pendingJoins = pendingJoins;
        _worldHost = worldHost;
        _config = config;
        _hostInfo = hostInfo;
        _claims = claims;
        _logger = loggerFactory.CreateLogger<LanHost>();
        _loggerFactory = loggerFactory;
    }

    public void Run()
    {
        int port = _config.Server.Port.Get();
        _host = new TcpServerHost(_serializers, _loggerFactory, _config.Transport.TimeoutMs.Get(), _config.Transport.SendQueueSize.Get(), _config.Transport.MaxFrameBytes.Get(), _config.Transport.ReliableQueueConcernThreshold.Get(), _config.Transport.ReliableQueueDisconnectThreshold.Get(), _config.Transport.ReliableQueueDisconnectMs.Get(), _config.Transport.TraceLogging.Get(), _config.Transport.SendIntervalMs.Get());
        _host.OnClientAccepted = transport =>
        {
            _clientIds[transport] = 0;
            transport.OnDisconnected += reason => RemoveClient(transport, reason);
            _pendingJoins.Add(transport);
            _logger.LogInformation("A LAN client connected and awaits its join request.");

            //  A peer that dies between accept and this handler was raised before any subscriber
            //  existed, so OnDisconnected never fires for it. Re-check the live state and prune it now,
            //  before the client can linger in the pending set unremoved.
            if (!transport.IsConnected)
            {
                RemoveClient(transport, DisconnectReason.PeerClosed);
            }
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
        if (!_config.Discovery.Enabled.Get())
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
            int intervalMs = Math.Max(100, _config.Discovery.BroadcastSeconds.Get() * 1000);
            int tcpPort = _host!.LocalPort;
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

    private void RemoveClient(TcpTransport transport, DisconnectReason reason)
    {
        _logger.LogInformation("A LAN client disconnected: {reason}.", reason);

        //  The owner disposes the dead peer: dropping it from the pending set (or releasing its world
        //  binding), pruning the id map, and closing the socket exactly once per transport.
        if (_clientIds.TryRemove(transport, out _))
        {
            _claims.Clear(transport);
            if (!_pendingJoins.Remove(transport))
            {
                _worldHost.DetachConnection(transport);
            }

            transport.Dispose();
        }
    }

    public void Dispose()
    {
        StopBeacon();
        _host?.Dispose();
    }
}