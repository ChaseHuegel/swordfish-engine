namespace Swordfish.Library.Collections.Filtering;

public readonly struct MultiFilter<T>(IFilter<T> filter1, IFilter<T> filter2) : IFilter<T>
{
    public readonly bool Allowed(T value)
    {
        return filter1.Allowed(value) && filter2.Allowed(value);
    }

    public T[] Filter(T[] values)
    {
        return [.. filter1.Filter(values), .. filter2.Filter(values)];
    }
}