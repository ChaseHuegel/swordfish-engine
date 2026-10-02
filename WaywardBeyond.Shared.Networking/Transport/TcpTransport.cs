using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Library.Serialization;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Networking.Serialization;

namespace WaywardBeyond.Shared.Networking.Transport;

/// <summary>
/// Socket transport usable as either a client (<see cref="Connect"/>) or a server peer
/// (<see cref="Listen"/>). Every message is serialized through the wire format and framed with a
/// length prefix plus a type tag, then dispatched to a per-type queue on the receiving side — so a
/// peer can poll for several distinct message kinds independently without one polling loop stealing
/// another's frames. Transport framing only: no ack, ordering, or reliability.
/// </summary>
public sealed class TcpTransport : IClientConnection, IServerConnection, IDisposable
{
    private readonly SerializerCache _serializers;
    private readonly ILogger _logger;
    private readonly int _connectionTimeoutMs;
    private readonly int _sendQueueSize;
    private readonly ConcurrentDictionary<Type, ConcurrentQueue<byte[]>> _receiveQueues = new();
    private readonly BlockingCollection<byte[]> _sendQueue;
    private readonly CancellationTokenSource _sendCts = new();
    private TcpClient? _client;
    private TcpListener? _listener;
    private NetworkStream? _stream;
    private volatile bool _isRunning;
    private volatile bool _disconnectedRaised;
    private Thread? _receiveThread;
    private Thread? _sendThread;

    public bool IsConnected => _client?.Connected ?? false;
    public bool IsLocal => false;

    /// <summary>
    /// Raised once when the remote peer disconnects (receive loop reaches EOF/error) while the transport is
    /// still running — i.e. not on an intentional <see cref="Disconnect"/>. Used by the server host to drop
    /// a departed client from its connection hub.
    /// </summary>
    public Action? OnDisconnected { get; set; }

    public TcpTransport(
        IEnumerable<INetworkSerializer> serializers,
        ILoggerFactory? loggerFactory = null,
        int connectionTimeoutMs = 5000,
        int sendQueueSize = 256
    ) {
        _serializers = new SerializerCache(serializers);
        _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<TcpTransport>();
        _connectionTimeoutMs = connectionTimeoutMs;
        _sendQueueSize = Math.Max(1, sendQueueSize);
        _sendQueue = new BlockingCollection<byte[]>(_sendQueueSize);
    }

    /// <summary>
    /// Builds a transport that owns an already-accepted socket (used by the server host for each peer).
    /// </summary>
    public static TcpTransport Accepted(
        IEnumerable<INetworkSerializer> serializers,
        TcpClient client,
        ILoggerFactory? loggerFactory = null,
        int connectionTimeoutMs = 5000,
        int sendQueueSize = 256
    ) {
        var transport = new TcpTransport(serializers, loggerFactory, connectionTimeoutMs, sendQueueSize);
        transport._client = client;
        transport._client.NoDelay = true;
        transport._client.SendTimeout = connectionTimeoutMs;
        transport._client.ReceiveTimeout = connectionTimeoutMs;
        transport._stream = client.GetStream();
        transport.StartLoops();
        return transport;
    }

    /// <summary>The bound local port after <see cref="Listen"/>, or 0 if not listening.</summary>
    public int LocalPort => (_listener?.LocalEndpoint as IPEndPoint)?.Port ?? 0;

    public void Connect(string host, int port)
    {
        _client = new TcpClient();
        _client.Connect(host, port);
        _client.NoDelay = true;
        _client.SendTimeout = _connectionTimeoutMs;
        _client.ReceiveTimeout = _connectionTimeoutMs;
        _stream = _client.GetStream();
        StartLoops();
    }

    public void Listen(int port)
    {
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        var acceptThread = new Thread(AcceptLoop)
        {
            IsBackground = true,
            Name = "TcpTransport Accept"
        };
        acceptThread.Start();
    }

    private void AcceptLoop()
    {
        try
        {
            _client = _listener!.AcceptTcpClient();
            _client.NoDelay = true;
            _client.SendTimeout = _connectionTimeoutMs;
            _client.ReceiveTimeout = _connectionTimeoutMs;
            _stream = _client.GetStream();
            StartLoops();
        }
        catch
        {
            //  Listener stopped (Dispose/Disconnect) while waiting for a connection.
        }
    }

    public void Disconnect()
    {
        _isRunning = false;
        _sendCts.Cancel();
        _stream?.Close();
        _client?.Close();
        _listener?.Stop();
    }

    public Result Send<T>(in T message)
    {
        if (!_isRunning)
        {
            return Result.FromFailure("Transport is not running.");
        }
        if (!_serializers.TryGet<T>(out ISerializer<T> serializer))
        {
            return Result.FromFailure($"No serializer registered for type {typeof(T).Name}.");
        }
        if (!_serializers.TryGetTypeName<T>(out string typeName))
        {
            return Result.FromFailure($"No serializer registered for type {typeof(T).Name}.");
        }

        byte[] payload = serializer.Serialize(message);
        byte[] typeTag = Encoding.UTF8.GetBytes(typeName);

        //  Frame body = [4-byte type-tag length][type tag][payload], preceded on the wire by a
        //  [4-byte body length] prefix (the body length excludes the prefix itself).
        byte[] frame = new byte[4 + typeTag.Length + payload.Length];
        BitConverter.TryWriteBytes(frame.AsSpan(0, 4), typeTag.Length);
        typeTag.CopyTo(frame, 4);
        payload.CopyTo(frame, 4 + typeTag.Length);

        var bytes = new byte[4 + frame.Length];
        BitConverter.TryWriteBytes(bytes.AsSpan(0, 4), frame.Length);
        frame.CopyTo(bytes, 4);

        //  Enqueue for the dedicated send thread. Sends never block the calling thread. When the queue is
        //  full (peer stopped reading a dead socket) drop the oldest frame and retry the new one so input
        //  staleness is bounded instead of the queue growing without limit.
        if (_sendQueue.TryAdd(bytes))
        {
            return Result.FromSuccess();
        }

        _sendQueue.TryTake(out _);
        if (_sendQueue.TryAdd(bytes))
        {
            return Result.FromSuccess();
        }

        return Result.FromFailure("Send queue is full.");
    }

    public Result<T> Receive<T>()
    {
        if (!_serializers.TryGet<T>(out ISerializer<T> serializer))
        {
            return Result<T>.FromFailure($"No serializer registered for type {typeof(T).Name}.");
        }

        if (!_receiveQueues.TryGetValue(typeof(T), out ConcurrentQueue<byte[]>? queue) || !queue.TryDequeue(out byte[]? data))
        {
            return Result<T>.FromFailure("No messages available.");
        }

        try
        {
            return Result<T>.FromSuccess(serializer.Deserialize(data));
        }
        catch (Exception ex)
        {
            return Result<T>.FromFailure(ex);
        }
    }

    public void Dispose()
    {
        Disconnect();
        _sendCts.Dispose();
        _sendQueue.Dispose();
    }

    private void StartLoops()
    {
        _isRunning = true;
        _receiveThread = new Thread(ReceiveLoop)
        {
            IsBackground = true,
            Name = "TcpTransport Receive"
        };
        _receiveThread.Start();

        _sendThread = new Thread(SendLoop)
        {
            IsBackground = true,
            Name = "TcpTransport Send"
        };
        _sendThread.Start();
    }

    private void SendLoop()
    {
        while (_isRunning)
        {
            byte[] bytes;
            try
            {
                bytes = _sendQueue.Take(_sendCts.Token);
            }
            catch (OperationCanceledException)
            {
                break; //  Intentional Disconnect.
            }

            if (!_isRunning)
            {
                break;
            }

            try
            {
                _stream?.Write(bytes, 0, bytes.Length);
            }
            catch (Exception)
            {
                //  A write timeout or socket failure means the peer is gone. Surface it exactly once.
                MarkBroken();
                break;
            }
        }
    }

    private void ReceiveLoop()
    {
        byte[] lengthBuffer = new byte[4];

        while (_isRunning)
        {
            try
            {
                if (ReadExact(lengthBuffer, 0, 4) == 0)
                {
                    break;
                }

                int frameLength = BitConverter.ToInt32(lengthBuffer, 0);
                if (frameLength < 8)
                {
                    break;
                }

                byte[] frame = new byte[frameLength];
                if (ReadExact(frame, 0, frameLength) == 0)
                {
                    break;
                }

                int typeTagLength = BitConverter.ToInt32(frame, 0);
                if (typeTagLength < 0 || 4 + typeTagLength > frameLength)
                {
                    break;
                }

                string typeName = Encoding.UTF8.GetString(frame, 4, typeTagLength);
                byte[] payload = new byte[frameLength - 4 - typeTagLength];
                Array.Copy(frame, 4 + typeTagLength, payload, 0, payload.Length);

                if (!_serializers.TryGetType(typeName, out Type type))
                {
                    _logger.LogWarning("Dropping frame with unknown type tag '{typeName}'.", typeName);
                    continue;
                }

                _receiveQueues.GetOrAdd(type, static _ => new ConcurrentQueue<byte[]>()).Enqueue(payload);
            }
            catch
            {
                break;
            }
        }

        //  If we exited the loop due to a peer disconnect (not an intentional Disconnect, which already
        //  cleared _isRunning), surface the disconnect so a host can drop the client from its hub and a
        //  client can return to the menu.
        if (_isRunning)
        {
            MarkBroken();
        }
    }

    /// <summary>
    /// Marks the transport broken after a peer disconnect detected by either the receive or the send
    /// thread, canceling the send drain and raising <see cref="OnDisconnected"/> exactly once.
    /// </summary>
    private void MarkBroken()
    {
        if (!_isRunning)
        {
            return;
        }

        _isRunning = false;
        _sendCts.Cancel();

        if (_disconnectedRaised)
        {
            return;
        }

        _disconnectedRaised = true;
        try
        {
            OnDisconnected?.Invoke();
        }
        catch
        {
            //  A subscriber's exception must not kill the detecting thread.
        }
    }

    private int ReadExact(byte[] buffer, int offset, int count)
    {
        int totalRead = 0;

        while (totalRead < count)
        {
            int read = _stream!.Read(buffer, offset + totalRead, count - totalRead);

            if (read == 0)
            {
                return totalRead;
            }

            totalRead += read;
        }

        return totalRead;
    }
}