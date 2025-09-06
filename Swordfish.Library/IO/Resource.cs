namespace Swordfish.Library.IO;

public readonly struct Resource<T>(PathInfo sourcePath, T value)
{
    public readonly PathInfo SourcePath = sourcePath;
    public readonly T Value = value;
}
