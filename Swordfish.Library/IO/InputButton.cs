// ReSharper disable UnusedMember.Global
namespace Swordfish.Library.IO;

public readonly struct InputButton(in int index, in string name)
{
    public readonly int Index = index;

    public readonly string Name = name;
}