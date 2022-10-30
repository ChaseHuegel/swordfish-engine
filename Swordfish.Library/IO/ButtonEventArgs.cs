
using System;

namespace Swordfish.Library.IO
{
    public class ButtonEventArgs : EventArgs
    {
        public new readonly ButtonEventArgs Empty = new ButtonEventArgs(new InputButton());

        public InputButton Button;

        public ButtonEventArgs(InputButton button)
        {
            Button = button;
        }
    }
}
