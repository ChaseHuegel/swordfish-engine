using System;
using Swordfish.Library.Types;

namespace Swordfish.Library.IO
{
    public class ClickedEventArgs : EventArgs
    {
        public new readonly ClickedEventArgs Empty = new ClickedEventArgs(MouseButton.NONE, Vec2f.Zero);

        public MouseButton MouseButton;
        public Vec2f Position;

        public ClickedEventArgs(MouseButton mouseButton, Vec2f position)
        {
            MouseButton = mouseButton;
            Position = position;
        }
    }
}
