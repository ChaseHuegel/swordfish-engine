using System;

namespace Swordfish.Library.Types
{
    public class DataBinding<T>
    {
        private T data;

        public EventHandler<DataChangedEventArgs<T>> Changed;

        public static implicit operator T(DataBinding<T> binding) => binding.Get();

        public DataBinding() { }

        public DataBinding(T value)
        {
            data = value;
        }

        public T Get() => data;

        public DataBinding<T> Set(T value)
        {
            if (data != null && !data.Equals(value))
            {
                Changed?.Invoke(this, new DataChangedEventArgs<T>(data, value));
                data = value;
                return this;
            }

            if (data == null && value != null)
            {
                Changed?.Invoke(this, new DataChangedEventArgs<T>(data, value));
                data = value;
                return this;
            }

            return this;
        }
    }
}
