
using System;

namespace Swordfish.Library.IO
{
    public class KeyEventArgs : EventArgs
    {
        public static new readonly KeyEventArgs Empty = new KeyEventArgs(Key.NONE);

        public Key Key;

        public KeyEventArgs(Key key)
        {
            Key = key;
        }
    }
}
