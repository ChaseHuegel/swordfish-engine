namespace Swordfish.Library.IO
{
    public interface IFileParser
    {
        string[] SupportedExtensions { get; }

        object Parse(IFileService fileService, PathInfo file);
    }

    public interface IFileParser<TResult> : IFileParser
    {
        new TResult Parse(IFileService fileService, PathInfo file);
    }
}
