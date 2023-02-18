
using System;

namespace Swordfish.Library.IO
{
    public class InputButtonEventArgs : EventArgs
    {
        public static new readonly InputButtonEventArgs Empty = new InputButtonEventArgs(new InputButton());

        public InputButton Button;

        public InputButtonEventArgs(InputButton button)
        {
            Button = button;
        }
    }
}
