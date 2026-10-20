using System;
using Microsoft.Extensions.Logging;
using NsdRpc;
using NsdRpc.Tcp;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Identity;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Networking.Rpc;
using WaywardBeyond.Networking.Transport;

namespace WaywardBeyond.Client.Networking;

/// <summary>
/// The client RPC endpoint. It owns the frozen client registry, the active <see cref="RpcClient"/>, the
/// generated <see cref="GameServerApiClient"/>, the connection, the inbound queues, and the connection
/// stats. Singleplayer keeps it on the in-process loopback. <see cref="ConnectRemote"/> swaps to TCP
/// and closes the loopback only after the new connection succeeds. A remote drop restores the
/// loopback, so the menu stays usable without an application restart.
/// </summary>
internal sealed class ClientRpcHost : IDisposable
{
    private readonly LoopbackRpcServerTransport _loopbackTransport;
    private readonly ClientInbound _inbound;
    private readonly RpcRegistry _registry;
    private readonly RpcOptions _options;
    private readonly NetworkingConfig _config;
    private readonly ProfileUserClaimProvider _userClaimProvider;
    private readonly ILogger<ClientRpcHost> _logger;
    private readonly object _gate = new();

    private IRpcConnection? _connection;
    private RpcClient? _client;
    private GameServerApiClient? _api;
    private bool _isLocal;
    private bool _disposed;

    /// <summary>Raised on a non-local close. The ECS thread performs the teardown.</summary>
    public event Action? RemoteDisconnected;

    public ClientRpcHost(
        LoopbackRpcServerTransport loopbackTransport,
        ClientInbound inbound,
        ClientHandlers handlers,
        NetworkingConfig config,
        ProfileUserClaimProvider userClaimProvider,
        ILoggerFactory loggerFactory
    ) {
        _loopbackTransport = loopbackTransport;
        _inbound = inbound;
        _config = config;
        _userClaimProvider = userClaimProvider;
        _logger = loggerFactory.CreateLogger<ClientRpcHost>();

        _registry = new RpcRegistry();
        _registry.AddService(handlers);
        _registry.Freeze();
        _options = GameRpcOptions.CreateRpc(config, loggerFactory.CreateLogger("NsdRpc"));

        //  Singleplayer is the default lifecycle.
        ConnectLocal();
    }

    /// <summary>The active server proxy, or null while no connection is up.</summary>
    public GameServerApiClient? Api => _api;

    /// <summary>The active transport connection. Tests use it to simulate a peer drop.</summary>
    internal IRpcConnection? ActiveConnection => _connection;

    /// <summary>True while the active connection is the in-process loopback.</summary>
    public bool IsLocal => _isLocal && _connection != null && _connection.IsConnected;

    /// <summary>True while the active connection is remote TCP.</summary>
    public bool IsRemote => !IsLocal && _connection != null && _connection.IsConnected;

    public bool IsConnected => _connection != null && _connection.IsConnected;

    public RpcConnectionStats Stats => _connection is IRpcConnectionStats stats ? stats.Stats : default;

    public RpcDisconnectReason? LastDisconnectReason { get; private set; }

    /// <summary>Checks RPC call deadlines. Runs once per client tick.</summary>
    public void Poll()
    {
        _client?.Poll();
    }

    /// <summary>Drops every staged inbound message. Call when leaving a world or returning to the menu.</summary>
    public void ClearInbound()
    {
        _inbound.Clear();
    }

    /// <summary>Connects to a remote host over TCP. The loopback closes only after the connect succeeds.</summary>
    public Result ConnectRemote(string host, int port)
    {
        try
        {
            TcpOptions tcpOptions = GameRpcOptions.CreateTcp(_config, host, port);
            var connection = new TcpClientConnection(_options, tcpOptions);
            connection.ConnectAsync().GetAwaiter().GetResult();

            lock (_gate)
            {
                IRpcConnection? previous = _connection;
                bool previousWasLocal = _isLocal;
                Activate(connection, isLocal: false);
                previous?.Close(RpcDisconnectReason.LocalClose);
                if (previousWasLocal)
                {
                    _logger.LogInformation("Closed the loopback connection for a remote session.");
                }
            }

            return Result.FromSuccess();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect to {host}:{port}.", host, port);
            return Result.FromFailure(ex);
        }
    }

    /// <summary>
    /// Drops a remote connection. An active loopback is left alone, so repeated calls are harmless.
    /// </summary>
    public void Disconnect()
    {
        lock (_gate)
        {
            if (_isLocal && _connection != null && _connection.IsConnected)
            {
                return;
            }

            IRpcConnection? connection = _connection;
            _connection = null;
            _client = null;
            _api = null;
            connection?.Close(RpcDisconnectReason.LocalClose);
            RestoreLoopback();
        }
    }

    /// <summary>Sends a typed notification on the active client. Returns false when the send is rejected.</summary>
    public bool Notify<TRequest>(ulong method, in TRequest request)
        where TRequest : struct
    {
        return _client?.Notify(method, in request) ?? false;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            IRpcConnection? connection = _connection;
            _connection = null;
            _client = null;
            _api = null;
            connection?.Close(RpcDisconnectReason.LocalClose);
        }
    }

    private void ConnectLocal()
    {
        lock (_gate)
        {
            if (_isLocal && _connection != null && _connection.IsConnected)
            {
                return;
            }

            var connection = new LoopbackRpcClientConnection(_loopbackTransport);
            connection.ConnectAsync().GetAwaiter().GetResult();
            Activate(connection, isLocal: true);
        }
    }

    private void Activate(IRpcConnection connection, bool isLocal)
    {
        connection.Disconnected += reason => OnDisconnected(connection, reason);

        var client = new RpcClient(connection, _registry, _options);
        _connection = connection;
        _client = client;
        _api = new GameServerApiClient(client);
        _isLocal = isLocal;

        _inbound.Clear();
        SendHello();
    }

    private void OnDisconnected(IRpcConnection connection, RpcDisconnectReason reason)
    {
        bool raiseRemoteDisconnected;
        lock (_gate)
        {
            if (!ReferenceEquals(connection, _connection))
            {
                return;
            }

            _connection = null;
            _client = null;
            _api = null;
            LastDisconnectReason = reason;
            raiseRemoteDisconnected = reason != RpcDisconnectReason.LocalClose;
        }

        _logger.LogInformation("Client RPC connection closed: {reason}.", reason);

        if (raiseRemoteDisconnected)
        {
            RemoteDisconnected?.Invoke();
        }

        RestoreLoopback();
    }

    private void RestoreLoopback()
    {
        lock (_gate)
        {
            if (_disposed || (_isLocal && _connection != null && _connection.IsConnected))
            {
                return;
            }

            try
            {
                ConnectLocal();
                _logger.LogInformation("Restored the loopback session.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore the loopback session.");
            }
        }
    }

    private void SendHello()
    {
        _api?.ClientHello(new ClientHello { UserId = _userClaimProvider.GetClaim().UserId });
    }
}
