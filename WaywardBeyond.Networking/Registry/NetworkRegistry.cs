using System;
using System.Collections.Generic;
using System.Threading;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Networking.Codecs;

namespace WaywardBeyond.Networking.Registry;

/// <summary>Maps networked component types to a stable wire uuid, an authoritative owner, and a payload codec.</summary>
public sealed class NetworkRegistry
{
    private readonly Dictionary<Type, NetworkComponentInfo> _byType = [];
    private readonly Dictionary<Uuid, NetworkComponentInfo> _byUuid = [];
    private readonly Lock _lock = new();
    private volatile NetworkComponentInfo[] _serverOwned = [];
    private volatile NetworkComponentInfo[] _clientOwned = [];
    private bool _frozen;

    /// <summary>Registers one networked component. Fails after <see cref="Freeze"/> or on a duplicate type or uuid.</summary>
    public Result Register<T>(Uuid uuid, NetworkDirection direction, IPayloadCodec<T> codec)
        where T : struct, IDataComponent
    {
        if (uuid == Uuid.Null)
        {
            return Result.FromFailure($"Cannot register {typeof(T).Name}: uuid is Null.");
        }

        lock (_lock)
        {
            if (_frozen)
            {
                return Result.FromFailure($"Cannot register {typeof(T).Name}: the registry is frozen.");
            }

            if (_byType.ContainsKey(typeof(T)))
            {
                return Result.FromFailure($"Cannot register {typeof(T).Name}: the type is already registered.");
            }

            if (_byUuid.ContainsKey(uuid))
            {
                return Result.FromFailure($"Cannot register {typeof(T).Name}: uuid {uuid} is already registered.");
            }

            var info = new NetworkComponentInfo(typeof(T), uuid, direction, codec);
            _byType[typeof(T)] = info;
            _byUuid[uuid] = info;
            return Result.FromSuccess();
        }
    }

    /// <summary>Closes the registry to further registration and caches the per-direction views.</summary>
    public void Freeze()
    {
        lock (_lock)
        {
            if (_frozen)
            {
                return;
            }

            _frozen = true;

            var serverOwned = new List<NetworkComponentInfo>();
            var clientOwned = new List<NetworkComponentInfo>();
            foreach (NetworkComponentInfo info in _byType.Values)
            {
                (info.Direction == NetworkDirection.ServerOwned ? serverOwned : clientOwned).Add(info);
            }

            _serverOwned = [.. serverOwned];
            _clientOwned = [.. clientOwned];
        }
    }

    /// <summary>Looks up one registered component type, or fails when it is not registered.</summary>
    public Result<NetworkComponentInfo> GetInfo<T>()
        where T : struct, IDataComponent
    {
        return GetInfo(typeof(T));
    }

    /// <summary>Looks up one registered component type, or fails when it is not registered.</summary>
    public Result<NetworkComponentInfo> GetInfo(Type type)
    {
        return _byType.TryGetValue(type, out NetworkComponentInfo info)
            ? Result<NetworkComponentInfo>.FromSuccess(info)
            : Result<NetworkComponentInfo>.FromFailure((string?)null);
    }

    /// <summary>Looks up one registered component by wire uuid, or fails when it is not registered.</summary>
    public Result<NetworkComponentInfo> GetInfo(Uuid uuid)
    {
        return _byUuid.TryGetValue(uuid, out NetworkComponentInfo info)
            ? Result<NetworkComponentInfo>.FromSuccess(info)
            : Result<NetworkComponentInfo>.FromFailure((string?)null);
    }

    /// <summary>Returns the frozen components owned by <paramref name="direction"/>. Do not mutate the array.</summary>
    public NetworkComponentInfo[] GetComponents(NetworkDirection direction)
    {
        return direction == NetworkDirection.ServerOwned ? _serverOwned : _clientOwned;
    }
}
