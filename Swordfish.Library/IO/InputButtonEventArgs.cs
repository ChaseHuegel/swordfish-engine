
// ReSharper disable UnusedMember.Global
namespace Swordfish.Library.IO;

public readonly struct InputButtonEventArgs(InputButton button)
{
    public static readonly InputButtonEventArgs Empty = new(new InputButton());

    public readonly InputButton Button = button;
}