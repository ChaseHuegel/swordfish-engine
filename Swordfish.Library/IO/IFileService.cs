using System.IO;

namespace Swordfish.Library.IO
{
    public interface IFileService
    {
        Stream Open(IPath path);

        byte[] ReadBytes(IPath path);

        string ReadString(IPath path);

        void Write(IPath path, Stream stream);

        TResult Parse<TResult>(IPath path);

        TResult Parse<TResult>(Stream stream);

        bool TryParse<TResult>(IPath path, out TResult result);

        bool TryParse<TResult>(Stream stream, out TResult result);

        IPath[] GetFiles(IPath path);

        IPath[] GetFiles(IPath path, string searchPattern);

        IPath[] GetFiles(IPath path, SearchOption searchOption);

        IPath[] GetFiles(IPath path, string searchPattern, SearchOption searchOption);
    }
}
