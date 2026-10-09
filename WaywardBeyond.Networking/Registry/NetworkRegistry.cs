using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Swordfish.ECS;
using Swordfish.Library.Util;

namespace WaywardBeyond.Networking.Registry;

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

    public static Result Register<T>(Uuid uuid, NetworkDirection direction, IPayloadCodec codec)
        where T : struct, IDataComponent
    {
        return Register(typeof(T), uuid, direction, codec);
    }

    /// <summary>
    /// Scans the given <paramref name="assemblies"/> for <see cref="IDataComponent"/> structs annotated
    /// with <see cref="NetworkComponentAttribute"/> and registers them, using a
    /// <see cref="NsdComponentCodec{T}"/> derived from their generated nsd serializer.
    /// </summary>
    public static void Initialize(IEnumerable<Assembly> assemblies)
    {
        foreach (Assembly assembly in assemblies)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types.Where(t => t != null).ToArray()!;
            }

            foreach (Type type in types)
            {
                NetworkComponentAttribute? attribute = type.GetCustomAttribute<NetworkComponentAttribute>();
                if (!type.IsValueType
                    || !typeof(IDataComponent).IsAssignableFrom(type)
                    || attribute == null)
                {
                    continue;
                }

                Type codecType = typeof(NsdComponentCodec<>).MakeGenericType(type);
                IPayloadCodec codec = (IPayloadCodec)Activator.CreateInstance(codecType)!;
                Register(type, attribute.Uuid, attribute.Direction, codec);
            }
        }
    }

    /// <summary>
    /// Registers a networked component, returning a contextual failure message instead of failing
    /// silently. Codec validity is proven at registration time: an nsd codec whose type lacks the
    /// generated <c>Serialize</c>/<c>Deserialize</c> methods fails here (its validation lives in the
    /// static constructor, forced via <c>RunClassConstructor</c>), not mid-game on the wire.
    /// </summary>
    public static Result Register(Type type, Uuid uuid, NetworkDirection direction, IPayloadCodec codec)
    {
        if (uuid == Uuid.Null)
        {
            return Result.FromFailure($"Cannot register {type.Name}: uuid is Null.");
        }

        if (codec == null)
        {
            return Result.FromFailure($"Cannot register {type.Name}: codec is null.");
        }

        Type codecType = codec.GetType();
        if (codecType.IsGenericType
            && codecType.GetGenericTypeDefinition() == typeof(NsdComponentCodec<>))
        {
            //  Force the static constructor now: it validates the generated serializer methods and
            //  throws a descriptive error when they are missing.
            RuntimeHelpers.RunClassConstructor(codecType.TypeHandle);
        }

        lock (_lock)
        {
            if (_byType.ContainsKey(type))
            {
                return Result.FromFailure($"Cannot register {type.Name}: a component of this type is already registered.");
            }

            if (_byUuid.ContainsKey(uuid))
            {
                return Result.FromFailure($"Cannot register {type.Name}: uuid {uuid} is already registered.");
            }

            var info = new NetworkComponentInfo(type, uuid, direction, codec);
            _byType[type] = info;
            _byUuid[uuid] = info;
            return Result.FromSuccess();
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