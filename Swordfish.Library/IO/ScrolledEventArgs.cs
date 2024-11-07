// ReSharper disable UnusedMember.Global
namespace Swordfish.Library.IO;

public readonly struct ScrolledEventArgs(float delta)
{
    public static readonly ScrolledEventArgs Empty = new(0f);

    public readonly float Delta = delta;
}