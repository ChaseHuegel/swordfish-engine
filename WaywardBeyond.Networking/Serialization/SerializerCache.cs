using System;
using System.Collections.Generic;
using Swordfish.Library.Serialization;

namespace WaywardBeyond.Networking.Serialization;

/// <summary>
/// Indexes the <see cref="ISerializer{T}"/> implementations managed by the DI container,
/// keyed by their message type, mirroring the lookup a socket transport needs.
/// </summary>
public sealed class SerializerCache
{
    private readonly Dictionary<Type, object> _serializers;
    private readonly Dictionary<string, Type> _serializersByTypeName;
    private readonly Dictionary<Type, byte[]> _typeTags;

    public SerializerCache(IEnumerable<INetworkSerializer> serializers)
    {
        _serializers = new Dictionary<Type, object>();
        _serializersByTypeName = new Dictionary<string, Type>();
        _typeTags = new Dictionary<Type, byte[]>();
        foreach (INetworkSerializer serializer in serializers)
        {
            _serializers[serializer.MessageType] = serializer;
            _serializersByTypeName[serializer.MessageType.FullName!] = serializer.MessageType;
            //  Type tags resolve to bytes once at registration and are reused per send.
            _typeTags[serializer.MessageType] = System.Text.Encoding.UTF8.GetBytes(serializer.MessageType.FullName!);
        }
    }

    /// <summary>The pre-encoded wire type tag for a message type, or null when unregistered.</summary>
    public byte[]? TryGetTypeTag<T>()
    {
        return _typeTags.TryGetValue(typeof(T), out byte[]? bytes) ? bytes : null;
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