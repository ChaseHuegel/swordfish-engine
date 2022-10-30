using System;

namespace Swordfish.Library.IO
{
    public class ScrolledEventArgs : EventArgs
    {
        public new readonly ScrolledEventArgs Empty = new ScrolledEventArgs(0f);

        public float Delta;

        public ScrolledEventArgs(float delta)
        {
            Delta = delta;
        }
    }
}
