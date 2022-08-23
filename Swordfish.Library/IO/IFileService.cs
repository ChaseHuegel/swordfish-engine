using System;
using System.IO;

namespace Swordfish.Library.IO
{
    public interface IFileService
    {
        FileStream Read(IPath path);

        void Write(IPath path, Stream stream);
    }
}
