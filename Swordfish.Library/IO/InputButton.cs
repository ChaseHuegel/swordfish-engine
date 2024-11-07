namespace Swordfish.Library.IO;

public struct InputButton
{
    public int Index;

    public string Name;

    public InputButton(int index, string name)
    {
        Index = index;
        Name = name;
    }
}