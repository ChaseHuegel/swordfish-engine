using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Swordfish.Library.Util;
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

    //  Aggregate counters for the pair; each endpoint's counters are the same object (in-process frames
    //  are counted once, at the queueing boundary).
    public IConnectionCounters Counters { get; }

    public LocalConnection(IEnumerable<INetworkSerializer> serializers)
    {
        _serializers = new SerializerCache(serializers);

        var counters = new LocalConnectionCounters();
        Counters = counters;

        var serverEndpoint = new LocalConnectionEndpoint(_serializers, sendQueues: _serverToClient, receiveQueues: _clientToServer, counters: counters);
        var clientEndpoint = new LocalConnectionEndpoint(_serializers, sendQueues: _clientToServer, receiveQueues: _serverToClient, counters: counters);
        Server = serverEndpoint;
        Client = clientEndpoint;
    }

    private sealed class LocalConnectionCounters : IConnectionCounters
    {
        private long _packetsSent;
        private long _packetsReceived;
        private long _bytesSent;
        private long _bytesReceived;

        public long PacketsSent => Interlocked.Read(ref _packetsSent);
        public long PacketsReceived => Interlocked.Read(ref _packetsReceived);
        public long BytesSent => Interlocked.Read(ref _bytesSent);
        public long BytesReceived => Interlocked.Read(ref _bytesReceived);

        public void RecordSent(int bytes) { Interlocked.Increment(ref _packetsSent); Interlocked.Add(ref _bytesSent, bytes); }
        public void RecordReceived(int bytes) { Interlocked.Increment(ref _packetsReceived); Interlocked.Add(ref _bytesReceived, bytes); }
    }

    private sealed class LocalConnectionEndpoint : IClientConnection, IServerConnection, IConnectionCounters
    {
        private readonly SerializerCache _serializers;
        private readonly ConcurrentDictionary<Type, ConcurrentQueue<byte[]>> _sendQueues;
        private readonly ConcurrentDictionary<Type, ConcurrentQueue<byte[]>> _receiveQueues;
        private readonly LocalConnectionCounters _counters;

        public bool IsConnected => true;
        public bool IsLocal => true;

        public long PacketsSent => _counters.PacketsSent;
        public long PacketsReceived => _counters.PacketsReceived;
        public long BytesSent => _counters.BytesSent;
        public long BytesReceived => _counters.BytesReceived;

        public LocalConnectionEndpoint(
            SerializerCache serializers,
            ConcurrentDictionary<Type, ConcurrentQueue<byte[]>> sendQueues,
            ConcurrentDictionary<Type, ConcurrentQueue<byte[]>> receiveQueues,
            LocalConnectionCounters counters
        ) {
            _serializers = serializers;
            _sendQueues = sendQueues;
            _receiveQueues = receiveQueues;
            _counters = counters;
        }

        public Result Send<T>(in T message)
        {
            if (!_serializers.TryGet<T>(out ISerializer<T> serializer))
            {
                return Result.FromFailure($"No serializer registered for type {typeof(T).Name}.");
            }

            byte[] payload = serializer.Serialize(message);
            _counters.RecordSent(payload.Length);

            _sendQueues.GetOrAdd(typeof(T), static _ => new ConcurrentQueue<byte[]>())
                .Enqueue(payload);
            return Result.FromSuccess();
        }

        public Result SendRaw(in byte[] frame)
        {
            //  A raw frame carries its own type tag; route the payload into that type's queue.
            int tagLength = BitConverter.ToInt32(frame, 4);
            string tag = System.Text.Encoding.UTF8.GetString(frame, 8, tagLength);
            if (!_serializers.TryGetType(tag, out Type type))
            {
                return Result.FromFailure($"No serializer registered for type tag '{tag}'.");
            }

            var payload = new byte[frame.Length - 8 - tagLength];
            Array.Copy(frame, 8 + tagLength, payload, 0, payload.Length);
            _counters.RecordSent(payload.Length);
            _sendQueues.GetOrAdd(type, static _ => new ConcurrentQueue<byte[]>()).Enqueue(payload);
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

            _counters.RecordReceived(data.Length);

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