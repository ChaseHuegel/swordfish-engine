namespace Swordfish.Library.Collections.Filtering;

public interface IFilter<T>
{
    bool Allowed(T value);

    T[] Filter(T[] values);
}
