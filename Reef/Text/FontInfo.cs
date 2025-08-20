namespace Reef.Text;

public readonly struct FontInfo(string id, string path)
{
    public readonly string ID = id;
    public readonly string Path = path;
}