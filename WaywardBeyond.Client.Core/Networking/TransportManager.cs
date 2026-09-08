using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Networking.Serialization;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Client.Core.Networking;

/// <summary>
/// The client-facing transport, exposing a single stable <see cref="IClientConnection"/> that the game
/// systems bind to at construction while transparently forwarding to whichever transport is active.
/// Singleplayer keeps it on the in-process <see cref="LocalConnection.Client"/>; multiplayer <see
/// cref="ConnectRemote"/> swaps it to a <see cref="TcpTransport"/> connected to a remote host so the
/// save select / character select / join flows target that server unchanged.
/// </summary>
internal sealed class TransportManager : IClientConnection
{
    private readonly IEnumerable<INetworkSerializer> _serializers;
    private readonly ILoggerFactory _loggerFactory;
    private TcpTransport? _remote;
    private IClientConnection? _active;

    public TransportManager(
        in IEnumerable<INetworkSerializer> serializers,
        in ILoggerFactory loggerFactory
    ) {
        _serializers = serializers;
        _loggerFactory = loggerFactory;
    }

    /// <summary>The currently active transport, or null before a client connects.</summary>
    public IClientConnection? Active => _active;

    /// <summary>Binds the transport to the in-process singleplayer connection and drops any remote one.</summary>
    public void UseLocal(in IClientConnection localConnection)
    {
        _remote?.Dispose();
        _remote = null;
        _active = localConnection;
    }

    /// <summary>Connects the transport to a remote host over TCP and drops any prior connection.</summary>
    public Result ConnectRemote(string host, int port)
    {
        try
        {
            var transport = new TcpTransport(_serializers, _loggerFactory);
            transport.Connect(host, port);

            _remote?.Dispose();
            _remote = transport;
            _active = transport;
            return Result.FromSuccess();
        }
        catch (Exception ex)
        {
            return Result.FromFailure(ex);
        }
    }

    /// <summary>Drops any active remote connection (e.g. returning to the menu).</summary>
    public void Disconnect()
    {
        _remote?.Dispose();
        _remote = null;
        _active = null;
    }

    public bool IsLocal => _active?.IsLocal ?? false;
    public bool IsConnected => _active?.IsConnected ?? false;

    public Result Send<T>(in T message)
    {
        return _active != null ? _active.Send(message) : Result.FromFailure("No active connection.");
    }

    public Result<T> Receive<T>()
    {
        return _active != null ? _active.Receive<T>() : Result<T>.FromFailure("No active connection.");
    }
}