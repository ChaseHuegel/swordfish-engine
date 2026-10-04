using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WaywardBeyond.Shared.Networking.Serialization;

namespace WaywardBeyond.Shared.Networking.Transport;

/// <summary>
/// Server-side TCP acceptor that lets a host serve multiple remote peers over a socket. Accepts
/// connections on a loop, wraps each accepted socket in its own <see cref="TcpTransport"/> (so each peer
/// has independent per-type queues and can be polled/routed independently), and hands it to <see
/// cref="OnClientAccepted"/> — the host wiring adds it to the <see cref="ServerConnectionHub"/>. The host
/// listens for <see cref="TcpTransport.OnDisconnected"/> to drop a departed peer from the hub; the
/// acceptor itself prunes that peer from its registry so it holds only live transports.
/// </summary>
public sealed class TcpServerHost : IDisposable
{
    private readonly IEnumerable<INetworkSerializer> _serializers;
    private readonly ILogger _logger;
    private readonly int _connectionTimeoutMs;
    private readonly int _sendQueueSize;
    private readonly int _keepaliveIntervalMs;
    private readonly int _maxFrameBytes;
    private readonly int _reliableQueueConcernThreshold;
    private readonly int _reliableQueueDisconnectThreshold;
    private readonly int _reliableQueueDisconnectMs;
    private readonly bool _traceLogging;
    private TcpListener? _listener;
    private Thread? _acceptThread;
    private volatile bool _isRunning;
    private readonly ConcurrentDictionary<TcpTransport, TcpTransport> _clients = new();

    /// <summary>Invoked with each freshly accepted server transport so a host can register it.</summary>
    public Action<TcpTransport>? OnClientAccepted { get; set; }

    public TcpServerHost(
        IEnumerable<INetworkSerializer> serializers,
        ILoggerFactory? loggerFactory = null,
        int connectionTimeoutMs = 5000,
        int sendQueueSize = 256,
        int keepaliveIntervalMs = 2000,
        int maxFrameBytes = 16 * 1024 * 1024,
        int reliableQueueConcernThreshold = 64,
        int reliableQueueDisconnectThreshold = 128,
        int reliableQueueDisconnectMs = 10_000,
        bool traceLogging = false
    ) {
        _serializers = serializers;
        _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<TcpServerHost>();
        _connectionTimeoutMs = connectionTimeoutMs;
        _sendQueueSize = sendQueueSize;
        _keepaliveIntervalMs = keepaliveIntervalMs;
        _maxFrameBytes = maxFrameBytes;
        _reliableQueueConcernThreshold = reliableQueueConcernThreshold;
        _reliableQueueDisconnectThreshold = reliableQueueDisconnectThreshold;
        _reliableQueueDisconnectMs = reliableQueueDisconnectMs;
        _traceLogging = traceLogging;
    }

    /// <summary>The bound local port after <see cref="Listen"/>, or 0 if not listening.</summary>
    public int LocalPort => (_listener?.LocalEndpoint as IPEndPoint)?.Port ?? 0;

    public void Listen(int port)
    {
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        _isRunning = true;
        _acceptThread = new Thread(AcceptLoop)
        {
            IsBackground = true,
            Name = "TcpServerHost Accept",
        };
        _acceptThread.Start();
    }

    private void AcceptLoop()
    {
        while (_isRunning)
        {
            TcpClient client;
            try
            {
                client = _listener!.AcceptTcpClient();
            }
            catch
            {
                break; //  Listener stopped (Dispose).
            }

            TcpTransport transport = TcpTransport.Accepted(_serializers, client, null, _connectionTimeoutMs, _sendQueueSize, _keepaliveIntervalMs, _maxFrameBytes, _reliableQueueConcernThreshold, _reliableQueueDisconnectThreshold, _reliableQueueDisconnectMs, _traceLogging);
            _clients[transport] = transport;
            transport.OnDisconnected += reason => _clients.TryRemove(transport, out _);
            try
            {
                OnClientAccepted?.Invoke(transport);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting a LAN client.");
            }
        }
    }

    public void Dispose()
    {
        _isRunning = false;
        _listener?.Stop();

        foreach (TcpTransport transport in _clients.Keys)
        {
            transport.Dispose();
        }

        _clients.Clear();
    }
}