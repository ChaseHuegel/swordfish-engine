// ReSharper disable UnusedMember.Global
namespace Swordfish.Library.IO;

public readonly struct CharEventArgs(char c)
{
    public static readonly CharEventArgs Empty = new('\0');

    public readonly char Char = c;
}