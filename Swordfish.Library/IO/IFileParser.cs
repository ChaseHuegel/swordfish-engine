namespace Swordfish.Library.IO;

public interface IFileParser
{
    string[] SupportedExtensions { get; }

    object Parse(PathInfo file);
}

public interface IFileParser<out TResult> : IFileParser
{
    new TResult Parse(PathInfo file);
}