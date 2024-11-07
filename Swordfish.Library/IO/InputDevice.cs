namespace Swordfish.Library.IO;

public struct InputDevice
{
    public int Index;

    public string Name;

    public InputDevice(int index, string name)
    {
        Index = index;
        Name = name;
    }
}