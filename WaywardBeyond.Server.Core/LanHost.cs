using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Shoal.Modularity;
using Swordfish.ECS;
using WaywardBeyond.Shared.Config;
using WaywardBeyond.Shared.Networking.Serialization;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Server.Core;

/// <summary>
/// Opens the in-process authoritative server to LAN peers over TCP. Runs alongside <see cref="ServerContext"/>
/// in host mode: it listens on <see cref="NetworkingSettings.ServerPort"/>, registers each accepted peer as a
/// client of the shared <see cref="ServerConnectionHub"/> (so sessions, per-client replication and despawn
/// routing all work), and removes a peer from the hub when its socket disconnects.
/// </summary>
public sealed class LanHost : IEntryPoint, IDisposable
{
    private readonly IEnumerable<INetworkSerializer> _serializers;
    private readonly ServerConnectionHub _hub;
    private readonly NetworkingSettings _settings;
    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConcurrentDictionary<TcpTransport, Uuid> _clientIds = new();
    private TcpServerHost? _host;

    public LanHost(
        in IEnumerable<INetworkSerializer> serializers,
        in ServerConnectionHub hub,
        in NetworkingSettings settings,
        ILoggerFactory loggerFactory
    ) {
        _serializers = serializers;
        _hub = hub;
        _settings = settings;
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start the LAN listener on port {port}.", port);
        }
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
        _host?.Dispose();
    }
}