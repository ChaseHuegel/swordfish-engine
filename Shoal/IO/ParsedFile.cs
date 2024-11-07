namespace Shoal.IO;

internal class ParsedFile<T>(PathInfo path, T value)
{
    public readonly PathInfo Path = path;
    public readonly T Value = value;
}