using System;
using Swordfish.Library.IO;

namespace Swordfish.Library.Types
{
    public interface IAsset : IDisposable
    {
        string Name { get; set; }

        PathInfo Path { get; }
    }
}
