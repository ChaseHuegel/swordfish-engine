using System;
using System.Collections.Generic;
using Swordfish.Library.Serialization;

namespace WaywardBeyond.Shared.Networking.Serialization;

/// <summary>
/// Indexes the <see cref="ISerializer{T}"/> implementations managed by the DI container,
/// keyed by their message type, mirroring the lookup a socket transport needs.
/// </summary>
public sealed class SerializerCache
{
    private readonly Dictionary<Type, object> _serializers;

    public SerializerCache(IEnumerable<INetworkSerializer> serializers)
    {
        _serializers = new Dictionary<Type, object>();
        foreach (INetworkSerializer serializer in serializers)
        {
            _serializers[serializer.MessageType] = serializer;
        }
    }

    public bool TryGet<T>(out ISerializer<T> serializer)
    {
        if (_serializers.TryGetValue(typeof(T), out object? serializerObj))
        {
            serializer = (ISerializer<T>)serializerObj;
            return true;
        }

        serializer = default!;
        return false;
    }
}