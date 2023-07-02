using System;
using System.Numerics;

namespace Swordfish.Library.IO
{
    public class ClickedEventArgs : EventArgs
    {
        public static new readonly ClickedEventArgs Empty = new ClickedEventArgs(MouseButton.UNKNOWN, Vector2.Zero);

        public MouseButton MouseButton;
        public Vector2 Position;

        public ClickedEventArgs(MouseButton mouseButton, Vector2 position)
        {
            MouseButton = mouseButton;
            Position = position;
        }
    }
}
