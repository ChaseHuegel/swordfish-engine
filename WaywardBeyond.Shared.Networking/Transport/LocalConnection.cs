using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Swordfish.Library.Serialization;
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
    private readonly SerializerCache _serializers;
    private readonly ConcurrentDictionary<Type, ConcurrentQueue<byte[]>> _clientToServer = new();
    private readonly ConcurrentDictionary<Type, ConcurrentQueue<byte[]>> _serverToClient = new();

    public IServerConnection Server { get; }
    public IClientConnection Client { get; }

    public LocalConnection(IEnumerable<INetworkSerializer> serializers)
    {
        _serializers = new SerializerCache(serializers);

        var serverEndpoint = new LocalConnectionEndpoint(_serializers, sendQueues: _serverToClient, receiveQueues: _clientToServer);
        var clientEndpoint = new LocalConnectionEndpoint(_serializers, sendQueues: _clientToServer, receiveQueues: _serverToClient);
        Server = serverEndpoint;
        Client = clientEndpoint;
    }

    private sealed class LocalConnectionEndpoint : IClientConnection, IServerConnection
    {
        private readonly SerializerCache _serializers;
        private readonly ConcurrentDictionary<Type, ConcurrentQueue<byte[]>> _sendQueues;
        private readonly ConcurrentDictionary<Type, ConcurrentQueue<byte[]>> _receiveQueues;

        public bool IsConnected => true;
        public bool IsLocal => true;

        public LocalConnectionEndpoint(
            SerializerCache serializers,
            ConcurrentDictionary<Type, ConcurrentQueue<byte[]>> sendQueues,
            ConcurrentDictionary<Type, ConcurrentQueue<byte[]>> receiveQueues
        ) {
            _serializers = serializers;
            _sendQueues = sendQueues;
            _receiveQueues = receiveQueues;
        }

        public Result Send<T>(in T message)
        {
            if (!_serializers.TryGet<T>(out ISerializer<T> serializer))
            {
                return Result.FromFailure($"No serializer registered for type {typeof(T).Name}.");
            }

            _sendQueues.GetOrAdd(typeof(T), static _ => new ConcurrentQueue<byte[]>())
                .Enqueue(serializer.Serialize(message));
            return Result.FromSuccess();
        }

        public Result<T> Receive<T>()
        {
            if (!_serializers.TryGet<T>(out ISerializer<T> serializer))
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
                return Result<T>.FromSuccess(serializer.Deserialize(data));
            }
            catch (Exception ex)
            {
                return Result<T>.FromFailure(ex);
            }
        }
    }
}