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
    private TcpClient? _client;
    private TcpListener? _listener;
    private NetworkStream? _stream;
    private readonly ConcurrentDictionary<Type, ConcurrentQueue<byte[]>> _receiveQueues = new();
    private readonly object _sendLock = new();
    private volatile bool _isRunning;
    private Thread? _receiveThread;

    public bool IsConnected => _client?.Connected ?? false;
    public bool IsLocal => false;

    public TcpTransport(IEnumerable<INetworkSerializer> serializers, ILoggerFactory? loggerFactory = null)
    {
        _serializers = new SerializerCache(serializers);
        _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<TcpTransport>();
    }

    /// <summary>The bound local port after <see cref="Listen"/>, or 0 if not listening.</summary>
    public int LocalPort => (_listener?.LocalEndpoint as IPEndPoint)?.Port ?? 0;

    public void Connect(string host, int port)
    {
        _client = new TcpClient();
        _client.Connect(host, port);
        _client.NoDelay = true;
        _stream = _client.GetStream();
        StartReceiveLoop();
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
            _stream = _client.GetStream();
            StartReceiveLoop();
        }
        catch
        {
            //  Listener stopped (Dispose/Disconnect) while waiting for a connection.
        }
    }

    public void Disconnect()
    {
        _isRunning = false;
        _stream?.Close();
        _client?.Close();
        _listener?.Stop();
    }

    public Result Send<T>(in T message)
    {
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
        byte[] lengthPrefix = BitConverter.GetBytes(frame.Length);

        lock (_sendLock)
        {
            try
            {
                _stream?.Write(lengthPrefix, 0, 4);
                _stream?.Write(frame, 0, frame.Length);
                return Result.FromSuccess();
            }
            catch (Exception ex)
            {
                return Result.FromFailure(ex);
            }
        }
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
    }

    private void StartReceiveLoop()
    {
        _isRunning = true;
        _receiveThread = new Thread(ReceiveLoop)
        {
            IsBackground = true,
            Name = "TcpTransport Receive"
        };
        _receiveThread.Start();
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