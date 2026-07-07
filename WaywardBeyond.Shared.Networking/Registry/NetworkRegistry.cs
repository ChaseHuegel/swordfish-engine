using System;
using System.Collections.Generic;
using System.Threading;
using Swordfish.ECS;

namespace WaywardBeyond.Shared.Networking.Registry;

public static class NetworkRegistry
{
    public const int MaxComponents = 256;

    private static readonly Dictionary<Type, int> _typeToBit = [];
    private static readonly Dictionary<int, Type> _bitToType = [];
    private static int _next;
    private static readonly Lock _lock = new();

    public static void Register<T>() where T : struct, IDataComponent
    {
        Type type = typeof(T);

        lock (_lock)
        {
            if (_typeToBit.ContainsKey(type))
            {
                return;
            }

            int bit = _next++;
            if (bit >= MaxComponents)
            {
                throw new InvalidOperationException(
                    $"NetworkRegistry has exceeded the maximum of {MaxComponents} component types. "
                    + "Increase MaxComponents or reduce the number of networked component types.");
            }

            _typeToBit[type] = bit;
            _bitToType[bit] = type;
        }
    }

    public static int GetBit<T>() where T : struct, IDataComponent
    {
        lock (_lock)
        {
            return _typeToBit[typeof(T)];
        }
    }

    public static int GetBit(Type type)
    {
        lock (_lock)
        {
            return _typeToBit[type];
        }
    }

    public static Type GetType(int bit)
    {
        lock (_lock)
        {
            return _bitToType[bit];
        }
    }

    public static int Count
    {
        get
        {
            lock (_lock)
            {
                return _next;
            }
        }
    }

    public static IEnumerable<KeyValuePair<Type, int>> GetAll()
    {
        lock (_lock)
        {
            return new Dictionary<Type, int>(_typeToBit);
        }
    }
}
