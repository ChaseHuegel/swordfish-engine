using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Swordfish.ECS;

namespace WaywardBeyond.Shared.Networking.Registry;

/// <summary>
/// Maps networked ECS component types to a stable on-wire <see cref="Uuid"/> identity, an
/// authoritative <see cref="NetworkDirection"/>, and the <see cref="IPayloadCodec"/> used to
/// (de)serialize their snapshots.
/// </summary>
public static class NetworkRegistry
{
    private static readonly Dictionary<Type, NetworkComponentInfo> _byType = [];
    private static readonly Dictionary<Uuid, NetworkComponentInfo> _byUuid = [];
    private static readonly Lock _lock = new();

    public static bool Register<T>(Uuid uuid, NetworkDirection direction, IPayloadCodec codec)
        where T : struct, IDataComponent
    {
        return Register(typeof(T), uuid, direction, codec);
    }

    public static bool Register(Type type, Uuid uuid, NetworkDirection direction, IPayloadCodec codec)
    {
        if (uuid == Uuid.Null)
        {
            return false;
        }

        lock (_lock)
        {
            if (_byType.ContainsKey(type) || _byUuid.ContainsKey(uuid))
            {
                return false;
            }

            var info = new NetworkComponentInfo(type, uuid, direction, codec);
            _byType[type] = info;
            _byUuid[uuid] = info;
            return true;
        }
    }

    public static bool TryGetInfo(Type type, out NetworkComponentInfo info)
    {
        lock (_lock)
        {
            return _byType.TryGetValue(type, out info);
        }
    }

    public static bool TryGetInfo(Uuid uuid, out NetworkComponentInfo info)
    {
        lock (_lock)
        {
            return _byUuid.TryGetValue(uuid, out info);
        }
    }

    public static bool TryGetInfo<T>([NotNullWhen(true)] out NetworkComponentInfo info)
        where T : struct, IDataComponent
    {
        return TryGetInfo(typeof(T), out info);
    }

    /// <summary>Enumerates every registered component in the given <paramref name="direction"/>.</summary>
    public static IReadOnlyCollection<NetworkComponentInfo> GetComponents(NetworkDirection direction)
    {
        lock (_lock)
        {
            var result = new List<NetworkComponentInfo>(_byType.Count);
            foreach (NetworkComponentInfo info in _byType.Values)
            {
                if (info.Direction == direction)
                {
                    result.Add(info);
                }
            }

            return result;
        }
    }
}