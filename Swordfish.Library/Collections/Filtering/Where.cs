using System;
using System.Linq;

namespace Swordfish.Library.Collections.Filtering;

public readonly struct Where<T>(Func<T, bool> predicate) : IFilter<T>
{
    private readonly Func<T, bool> _predicate = predicate;

    public readonly bool Allowed(T value)
    {
        return _predicate(value);
    }

    public T[] Filter(T[] values)
    {
        return values.Where(_predicate).ToArray();
    }
}