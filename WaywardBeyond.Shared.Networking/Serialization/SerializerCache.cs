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
    private readonly Dictionary<string, Type> _serializersByTypeName;

    public SerializerCache(IEnumerable<INetworkSerializer> serializers)
    {
        _serializers = new Dictionary<Type, object>();
        _serializersByTypeName = new Dictionary<string, Type>();
        foreach (INetworkSerializer serializer in serializers)
        {
            _serializers[serializer.MessageType] = serializer;
            _serializersByTypeName[serializer.MessageType.FullName!] = serializer.MessageType;
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

    /// <summary>Stable wire identifier for a message type (its FullName), used to frame the type on the wire.</summary>
    public bool TryGetTypeName<T>(out string typeName)
    {
        if (_serializers.ContainsKey(typeof(T)))
        {
            typeName = typeof(T).FullName!;
            return true;
        }

        typeName = string.Empty;
        return false;
    }

    /// <summary>Resolves a wire type tag back to its message type for per-type dispatch.</summary>
    public bool TryGetType(string typeName, out Type type)
    {
        return _serializersByTypeName.TryGetValue(typeName, out type!);
    }

    public bool Contains<T>()
    {
        return _serializers.ContainsKey(typeof(T));
    }
}