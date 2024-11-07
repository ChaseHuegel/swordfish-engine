using System.IO;

namespace Swordfish.Library.IO;

public interface IFileParseService
{
    TResult Parse<TResult>(PathInfo path);

    TResult Parse<TResult>(Stream stream);

    bool TryParse<TResult>(PathInfo path, out TResult result);

    bool TryParse<TResult>(Stream stream, out TResult result);
}