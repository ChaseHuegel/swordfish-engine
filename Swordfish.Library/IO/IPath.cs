using System;
using Swordfish.Library.Annotations;

namespace Swordfish.Library.IO
{
    public interface IPath
    {
        string OriginalString { get; }

        string Value { get; }

        [NotNull]
        string Scheme { get; }

        IPath At(string value);

        IPath At(params string[] values);

        IPath At(IPath path);

        IPath CreateDirectory();

        string ToString();
    }
}
