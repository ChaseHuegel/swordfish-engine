namespace Reef.Text;

public readonly struct FontInfo
{
    public readonly string ID;
    public readonly string Path;
    public readonly int MinUnicode;
    public readonly int MaxUnicode;

    public FontInfo(string id, string path)
    {
        ID = id;
        Path = path;
        MinUnicode = 0x0000;
        MaxUnicode = 0xffff;
    }
    
    public FontInfo(string id, string path, int minUnicode, int maxUnicode)
    {
        ID = id;
        Path = path;
        MinUnicode = minUnicode;
        MaxUnicode = maxUnicode;
    }
}