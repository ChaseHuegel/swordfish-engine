using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Swordfish.Library.Serialization;
using Swordfish.Library.Util;

namespace WaywardBeyond.Shared.Networking.Transport;

public sealed class TcpTransport : INetworkTransport, IDisposable
{
    private readonly Dictionary<Type, object> _serializers = new();
    private TcpClient? _client;
    private TcpListener? _listener;
    private NetworkStream? _stream;
    private readonly ConcurrentQueue<byte[]> _receiveQueue = new();
    private readonly object _sendLock = new();
    private volatile bool _isRunning;
    private Thread? _receiveThread;

    public bool IsConnected => _client?.Connected ?? false;
    public bool IsLocal => false;

    public TcpTransport(object[] serializers)
    {
        for (int i = 0; i < serializers.Length; i++)
        {
            object serializer = serializers[i];
            Type type = serializer.GetType();

            foreach (Type iface in type.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(ISerializer<>))
                {
                    Type key = iface.GetGenericArguments()[0];
                    _serializers[key] = serializer;
                }
            }
        }
    }

    public void Connect(string host, int port)
    {
        _client = new TcpClient();
        _client.Connect(host, port);
        _stream = _client.GetStream();
        StartReceiveLoop();
    }

    public void Listen(int port)
    {
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        _client = _listener.AcceptTcpClient();
        _stream = _client.GetStream();
        StartReceiveLoop();
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
        if (!_serializers.TryGetValue(typeof(T), out object? serializerObj))
        {
            return Result.FromFailure($"No serializer registered for type {typeof(T).Name}.");
        }

        ISerializer<T> serializer = (ISerializer<T>)serializerObj;
        byte[] data = serializer.Serialize(message);

        lock (_sendLock)
        {
            try
            {
                byte[] lengthPrefix = BitConverter.GetBytes(data.Length);
                _stream?.Write(lengthPrefix, 0, 4);
                _stream?.Write(data, 0, data.Length);
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
        if (!_serializers.TryGetValue(typeof(T), out object? serializerObj))
        {
            return Result<T>.FromFailure($"No serializer registered for type {typeof(T).Name}.");
        }

        if (!_receiveQueue.TryDequeue(out byte[]? data))
        {
            return Result<T>.FromFailure("No messages available.");
        }

        try
        {
            ISerializer<T> serializer = (ISerializer<T>)serializerObj;
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

                int length = BitConverter.ToInt32(lengthBuffer, 0);
                byte[] data = new byte[length];

                if (ReadExact(data, 0, length) == 0)
                {
                    break;
                }

                _receiveQueue.Enqueue(data);
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
