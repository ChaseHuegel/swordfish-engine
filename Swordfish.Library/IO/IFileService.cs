using System.IO;

namespace Swordfish.Library.IO
{
    public interface IFileService
    {
        Stream Open(PathInfo path);

        byte[] ReadBytes(PathInfo path);

        string ReadString(PathInfo path);

        void Write(PathInfo path, Stream stream);

        TResult Parse<TResult>(PathInfo path);

        TResult Parse<TResult>(Stream stream);

        bool TryParse<TResult>(PathInfo path, out TResult result);

        bool TryParse<TResult>(Stream stream, out TResult result);

        PathInfo[] GetFiles(PathInfo path);

        PathInfo[] GetFiles(PathInfo path, string searchPattern);

        PathInfo[] GetFiles(PathInfo path, SearchOption searchOption);

        PathInfo[] GetFiles(PathInfo path, string searchPattern, SearchOption searchOption);
    }
}
