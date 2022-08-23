using System;

namespace Swordfish.Library.IO
{
    public interface IPathService
    {
        IPath Root { get; }

        IPath Plugins { get; }

        IPath Mods { get; }

        IPath Config { get; }

        IPath Screenshots { get; }

        IPath Shaders { get; }

        IPath UI { get; }

        IPath Resources { get; }

        IPath Fonts { get; }

        IPath Icons { get; }

        IPath Models { get; }

        IPath Textures { get; }
    }
}
