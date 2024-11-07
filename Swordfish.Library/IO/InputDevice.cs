// ReSharper disable UnusedMember.Global
namespace Swordfish.Library.IO;

public readonly struct InputDevice(int index, string name)
{
    public readonly int Index = index;

    public readonly string Name = name;
}