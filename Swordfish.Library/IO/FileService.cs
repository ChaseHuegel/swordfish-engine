using System;
using System.IO;

namespace Swordfish.Library.IO
{
    public class FileService : IFileService
    {
        public FileStream Read(IPath path)
        {
            throw new NotImplementedException();
        }

        public void Write(IPath path, Stream stream)
        {
            throw new NotImplementedException();
        }
    }
}
