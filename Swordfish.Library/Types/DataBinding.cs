using System;

namespace Swordfish.Library.Types;

public class DataBinding<T>
{
    private T _data;

    public EventHandler<DataChangedEventArgs<T>> Changed;

    public static implicit operator T(DataBinding<T> binding) => binding.Get();

    public DataBinding() { }

    public DataBinding(T value)
    {
        _data = value;
    }

    public T Get() => _data;

    public DataBinding<T> Set(T value)
    {
        if (_data != null && !_data.Equals(value))
        {
            Changed?.Invoke(this, new DataChangedEventArgs<T>(_data, value));
            _data = value;
            return this;
        }

        if (_data == null && value != null)
        {
            Changed?.Invoke(this, new DataChangedEventArgs<T>(_data, value));
            _data = value;
            return this;
        }

        return this;
    }
}