using System.IO;

namespace Swordfish.Library.IO
{
    public interface IFileService
    {
        Stream Read(IPath path);

        void Write(IPath path, Stream stream);

        TResult Parse<TResult>(IPath path);

        TResult Parse<TResult>(Stream stream);

        bool TryParse<TResult>(IPath path, out TResult result);

        bool TryParse<TResult>(Stream stream, out TResult result);
    }
}
