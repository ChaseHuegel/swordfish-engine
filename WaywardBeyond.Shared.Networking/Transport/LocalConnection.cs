using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Networking.Serialization;

namespace WaywardBeyond.Shared.Networking.Transport;

/// <summary>
/// In-process, bidirectional client-server connection. It serializes every message through the wire
/// format and relays the resulting frames between a client endpoint and a server endpoint, exercising
/// the full network protocol without a socket. Messages are buffered per type so a side can poll for
/// several distinct message kinds independently.
/// </summary>
public sealed class LocalConnection
{
    private readonly Dictionary<Type, INetworkSerializer> _serializers;
    private readonly ConcurrentDictionary<Type, ConcurrentQueue<byte[]>> _clientToServer = new();
    private readonly ConcurrentDictionary<Type, ConcurrentQueue<byte[]>> _serverToClient = new();

    public INetworkTransport Server { get; }
    public INetworkTransport Client { get; }

    public LocalConnection(IEnumerable<INetworkSerializer> serializers)
    {
        _serializers = new Dictionary<Type, INetworkSerializer>();
        foreach (INetworkSerializer serializer in serializers)
        {
            _serializers[serializer.MessageType] = serializer;
        }

        Server = new LocalConnectionEndpoint(_serializers, sendQueues: _serverToClient, receiveQueues: _clientToServer);
        Client = new LocalConnectionEndpoint(_serializers, sendQueues: _clientToServer, receiveQueues: _serverToClient);
    }

    private sealed class LocalConnectionEndpoint : INetworkTransport
    {
        private readonly Dictionary<Type, INetworkSerializer> _serializers;
        private readonly ConcurrentDictionary<Type, ConcurrentQueue<byte[]>> _sendQueues;
        private readonly ConcurrentDictionary<Type, ConcurrentQueue<byte[]>> _receiveQueues;

        public bool IsConnected => true;
        public bool IsLocal => true;

        public LocalConnectionEndpoint(
            Dictionary<Type, INetworkSerializer> serializers,
            ConcurrentDictionary<Type, ConcurrentQueue<byte[]>> sendQueues,
            ConcurrentDictionary<Type, ConcurrentQueue<byte[]>> receiveQueues
        ) {
            _serializers = serializers;
            _sendQueues = sendQueues;
            _receiveQueues = receiveQueues;
        }

        public Result Send<T>(in T message)
        {
            if (!_serializers.TryGetValue(typeof(T), out INetworkSerializer? serializer))
            {
                return Result.FromFailure($"No serializer registered for type {typeof(T).Name}.");
            }

            _sendQueues.GetOrAdd(typeof(T), static _ => new ConcurrentQueue<byte[]>())
                .Enqueue(serializer.Serialize(message!));
            return Result.FromSuccess();
        }

        public Result<T> Receive<T>()
        {
            if (!_serializers.TryGetValue(typeof(T), out INetworkSerializer? serializer))
            {
                return Result<T>.FromFailure($"No serializer registered for type {typeof(T).Name}.");
            }

            ConcurrentQueue<byte[]> queue = _receiveQueues.GetOrAdd(typeof(T), static _ => new ConcurrentQueue<byte[]>());
            if (!queue.TryDequeue(out byte[]? data))
            {
                return Result<T>.FromFailure("No messages available.");
            }

            try
            {
                return Result<T>.FromSuccess((T)serializer.Deserialize(data));
            }
            catch (Exception ex)
            {
                return Result<T>.FromFailure(ex);
            }
        }
    }
}