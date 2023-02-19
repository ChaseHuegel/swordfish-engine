using System.IO;

namespace Swordfish.Library.IO
{
    public interface IFileService
    {
        Stream Read(IPath path);

        void Write(IPath path, Stream stream);

        TResult Parse<TResult>(IPath path);

        bool TryParse<TResult>(IPath path, out TResult result);
    }
}
