using System.Numerics;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.IO;

public struct ClickedEventArgs(in MouseButton mouseButton, in Vector2 position)
{
    public static readonly ClickedEventArgs Empty = new(MouseButton.Unknown, Vector2.Zero);

    public readonly MouseButton MouseButton = mouseButton;
    public readonly Vector2 Position = position;
}