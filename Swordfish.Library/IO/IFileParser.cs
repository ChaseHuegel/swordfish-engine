
using System.IO;

namespace Swordfish.Library.IO
{
    public interface IFileParser
    {
        string[] SupportedExtensions { get; }

        object Parse(IFileService fileService, IPath file);
    }

    public interface IFileParser<TResult> : IFileParser
    {
        new TResult Parse(IFileService fileService, IPath file);
    }
}
