using System;
using System.Collections.Generic;
using Swordfish.Library.Serialization;

namespace WaywardBeyond.Shared.Networking.Serialization;

/// <summary>
/// Indexes the <see cref="ISerializer{T}"/> implementations discovered in an <see cref="object"/>[]
/// (registered with DryIoc) by their message type, mirroring the lookup a socket transport needs.
/// </summary>
public sealed class SerializerCache
{
    private readonly Dictionary<Type, object> _serializers;

    public SerializerCache(object[] serializers)
    {
        _serializers = new Dictionary<Type, object>();
        for (int i = 0; i < serializers.Length; i++)
        {
            object serializer = serializers[i];
            Type type = serializer.GetType();

            foreach (Type iface in type.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(ISerializer<>))
                {
                    _serializers[iface.GetGenericArguments()[0]] = serializer;
                }
            }
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