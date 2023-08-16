using System.Collections.Generic;

namespace Swordfish.Library.Collections
{
    public class ReadOnlyQueue<T>
    {
        private readonly Queue<T> Queue;

        public ReadOnlyQueue(params T[] values)
        {
            Queue = new Queue<T>(values);
        }

        public T Take()
        {
            return Queue.Dequeue();
        }

        public T Peek()
        {
            return Queue.Peek();
        }

        public T[] TakeAll()
        {
            T[] values = new T[Queue.Count];
            for (int i = 0; i < values.Length; i++)
                values[i] = Queue.Dequeue();

            return values;
        }
    }
}
