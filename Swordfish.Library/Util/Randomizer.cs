using System;
using System.Collections.Generic;

// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.Util;

public class Randomizer
{
    private readonly Random _random;

    public Randomizer()
    {
        _random = new Random();
    }
    
    public Randomizer(int seed)
    {
        _random = new Random(seed);
    }

    public float NextFloat()
    {
        return (float)_random.NextDouble();
    }

    public double NextDouble()
    {
        return _random.NextDouble();
    }

    public int NextInt()
    {
        return _random.Next();
    }

    public int NextInt(int maxValue)
    {
        return _random.Next(maxValue);
    }

    public int NextInt(int minValue, int maxValue)
    {
        return _random.Next(minValue, maxValue);
    }

    public T Select<T>(params T[] items)
    {
        int index = _random.Next(items.Length);
        return items[index];
    }

    public T Select<T>(List<T> list)
    {
        int index = _random.Next(list.Count);
        return list[index];
    }
}