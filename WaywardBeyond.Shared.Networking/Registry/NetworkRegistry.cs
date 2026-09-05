using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Swordfish.ECS;

namespace WaywardBeyond.Shared.Networking.Registry;

public static class NetworkRegistry
{
    private static readonly Dictionary<Type, Uuid> _typeToUuid = [];
    private static readonly Dictionary<Uuid, Type> _uuidToType = [];
    private static readonly Lock _lock = new();

    public static void Register<T>(Uuid uuid) where T : struct, IDataComponent
    {
        Type type = typeof(T);

        lock (_lock)
        {
            if (_typeToUuid.ContainsKey(type))
            {
                return;
            }
            
            if (_uuidToType.ContainsKey(uuid))
            {
                return;
            }

            _typeToUuid[type] = uuid;
            _uuidToType[uuid] = type;
        }
    }

    public static bool TryGetUuid(Type type, out Uuid uuid)
    {
        lock (_lock)
        {
            return _typeToUuid.TryGetValue(type, out uuid);
        }
    }
    
    public static bool TryGetType(Uuid uuid, [NotNullWhen(true)] out Type? type)
    {
        lock (_lock)
        {
            return _uuidToType.TryGetValue(uuid, out type);
        }
    }
}
