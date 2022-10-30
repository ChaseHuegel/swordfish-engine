
using System;

namespace Swordfish.Library.IO
{
    public class KeyEventArgs : EventArgs
    {
        public new readonly KeyEventArgs Empty = new KeyEventArgs(IO.Key.NONE);

        public Key Key;

        public KeyEventArgs(Key key)
        {
            Key = key;
        }
    }
}
