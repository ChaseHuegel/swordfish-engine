namespace Shoal.IO;

internal class ParsedFile<T>(IPath path, T value)
{
    public readonly IPath Path = path;
    public readonly T Value = value;
}