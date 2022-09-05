using System;

namespace Swordfish.Library.Types
{
    public class DataBinding<T>
    {
        private T data;

        public EventHandler<EventArgs> Changed;

        public static implicit operator T(DataBinding<T> binding) => binding.Get();

        public DataBinding() { }

        public DataBinding(T value)
        {
            data = value;
        }

        public T Get() => data;

        public DataBinding<T> Set(T value)
        {
            data = value;
            Changed?.Invoke(this, EventArgs.Empty);

            return this;
        }
    }
}
