
// ReSharper disable UnusedMember.Global
namespace Swordfish.Library.IO;

public readonly struct KeyEventArgs(Key key)
{
    public static readonly KeyEventArgs Empty = new(Key.None);

    public readonly Key Key = key;
}