using System;
using System.Threading;
using Microsoft.Extensions.Logging;
using NsdRpc;
using NsdRpc.Tcp;
using Shoal.Modularity;
using Swordfish.Library.Threading;
using WaywardBeyond.Config;
using WaywardBeyond.Networking.Rpc;
using WaywardBeyond.Networking.Transport;

namespace WaywardBeyond.Server;

/// <summary>
/// Owns the server RPC endpoints: one frozen registry with <see cref="ServerHandlers"/>, the in-process
/// loopback server, and the LAN server over TCP. It starts both, wires connect and disconnect events,
/// polls call deadlines on a background thread, and closes both on dispose. The client and server
/// peers are recorded in <see cref="ServerPeers"/>.
/// </summary>
public sealed class ServerRpcHost : IEntryPoint, IDisposable
{
    private readonly LoopbackRpcServerTransport _loopbackTransport;
    private readonly RecordingRpcServerTransport _loopbackRecorder;
    private readonly ServerPeers _peers;
    private readonly NetworkingConfig _config;
    private readonly RpcOptions _options;
    private readonly RpcRegistry _registry;
    private readonly ILogger<ServerRpcHost> _logger;
    private readonly RpcServer _loopback;
    private readonly ThreadWorker _pollWorker;
    private RecordingRpcServerTransport? _lanTransport;
    private RpcServer? _lan;
    private int _lanPort;
    private bool _disposed;

    /// <summary>Builds the server endpoints from the protocol and transport config.</summary>
    public ServerRpcHost(
        LoopbackRpcServerTransport loopbackTransport,
        ServerPeers peers,
        ServerHandlers handlers,
        NetworkingConfig config,
        ILoggerFactory loggerFactory
    ) {
        _loopbackTransport = loopbackTransport;
        _loopbackRecorder = new RecordingRpcServerTransport(loopbackTransport);
        _peers = peers;
        _config = config;
        _logger = loggerFactory.CreateLogger<ServerRpcHost>();
        _options = config.ToRpcOptions(RpcLogging.ToLogSink(loggerFactory.CreateLogger("NsdRpc")));

        _registry = new RpcRegistry();
        _registry.AddService(handlers);
        _registry.Freeze();

        _loopback = new RpcServer(_loopbackRecorder, _registry, _options);
        _peers.RegisterHost(_loopback, isLocal: true);
        _loopback.ClientConnected += peer => OnClientConnected(_loopback, peer, isLocal: true);
        _loopback.ClientDisconnected += peer => OnClientDisconnected(_loopback, peer, isLocal: true);

        _pollWorker = new ThreadWorker(Poll, "Server RPC Poll")
        {
            TargetTickRate = 32,
        };
    }

    /// <summary>The bound LAN port, or zero while the LAN server is not running.</summary>
    public int LanPort => Volatile.Read(ref _lanPort);

    /// <inheritdoc/>
    public void Run()
    {
        _loopback.Start();
        _pollWorker.Start();
        _logger.LogInformation("Started the in-process RPC server.");
    }

    /// <summary>Starts the LAN RPC listener on <paramref name="port"/>. Zero binds an ephemeral port.</summary>
    public void StartLan(int port)
    {
        if (_lan != null)
        {
            _logger.LogWarning("The LAN RPC server is already running.");
            return;
        }

        TcpOptions tcpOptions = _config.ToTcpOptions(host: "0.0.0.0", port: port);
        var tcp = new TcpServerTransport(_options, tcpOptions);
        _lanTransport = new RecordingRpcServerTransport(tcp);
        _lan = new RpcServer(_lanTransport, _registry, _options);
        _peers.RegisterHost(_lan, isLocal: false);
        _lan.ClientConnected += peer => OnClientConnected(_lan, peer, isLocal: false);
        _lan.ClientDisconnected += peer => OnClientDisconnected(_lan, peer, isLocal: false);
        _lan.Start();
        Volatile.Write(ref _lanPort, tcp.Port);

        _logger.LogInformation("Listening for LAN RPC connections on port {port}.", tcp.Port);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _pollWorker.Stop();
        _pollWorker.Join();
        _lan?.Close();
        _loopback.Close();
    }

    private void Poll()
    {
        _loopback.Poll();
        _lan?.Poll();
    }

    private void OnClientConnected(IRpcHost host, RpcPeer peer, bool isLocal)
    {
        ServerPeer serverPeer = _peers.GetOrAdd(host, peer, isLocal);
        _logger.LogInformation(
            "RPC client {clientId} connected ({endpoint}) as game peer {gameId}.",
            peer.ClientId, peer.RemoteEndPoint?.ToString() ?? "in-process", serverPeer.GameId);
    }

    private void OnClientDisconnected(IRpcHost host, RpcPeer peer, bool isLocal)
    {
        RpcDisconnectReason reason = RpcDisconnectReason.PeerClosed;
        RecordingRpcServerTransport recorder = isLocal ? _loopbackRecorder : _lanTransport!;
        if (recorder.TryTakeReason(peer.ClientId, out RpcDisconnectReason recorded))
        {
            reason = recorded;
        }

        ServerPeer? serverPeer = _peers.Remove(host, peer.ClientId);
        if (serverPeer == null)
        {
            return;
        }

        serverPeer.LastDisconnectReason = reason;
        if (!serverPeer.TryMarkDisconnectHandled())
        {
            return;
        }

        _peers.EnqueueDisconnected(serverPeer);
        _logger.LogInformation(
            "RPC client {clientId} ({endpoint}) disconnected: {reason}.",
            peer.ClientId, peer.RemoteEndPoint?.ToString() ?? "in-process", reason);
    }
}
