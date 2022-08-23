using System;

namespace Swordfish.Library.IO
{
    public interface IPath
    {
        string OriginalString { get; set; }

        IPath At(string value);

        IPath At(params string[] values);

        IPath At(IPath path);

        IPath CreateDirectory();
    }
}
