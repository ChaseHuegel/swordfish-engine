using System;

namespace Swordfish.Library.Types
{
    public class DataChangedEventArgs<T> : EventArgs
    {
        public static new readonly DataChangedEventArgs<T> Empty = new DataChangedEventArgs<T>();

        public readonly T OldValue;

        public readonly T NewValue;

        internal DataChangedEventArgs() { }

        public DataChangedEventArgs(T oldValue, T newValue)
        {
            OldValue = oldValue;
            NewValue = newValue;
        }
    }
}
