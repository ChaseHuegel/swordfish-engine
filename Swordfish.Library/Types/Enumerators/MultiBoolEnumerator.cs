using System;
using System.Collections;

namespace Swordfish.Library.Types.Enumerators
{
    public class MultiBoolEnumerator : IEnumerator
    {
        public MultiBool Value;

        private int index = -1;

        public MultiBoolEnumerator(MultiBool value)
        {
            Value = value;
        }

        object IEnumerator.Current => Current;

        public bool Current
        {
            get
            {
                try
                {
                    return Value[index];
                }
                catch
                {
                    throw new InvalidOperationException();
                }
            }
        }

        bool IEnumerator.MoveNext()
        {
            index++;
            return index < 8;
        }

        void IEnumerator.Reset()
        {
            index = -1;
        }
    }
}