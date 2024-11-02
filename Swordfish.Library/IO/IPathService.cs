using System;

namespace Swordfish.Library.IO
{
    public interface IPathService
    {
        PathInfo Root { get; }

        PathInfo Plugins { get; }

        PathInfo Mods { get; }

        PathInfo Config { get; }

        PathInfo Screenshots { get; }

        PathInfo Shaders { get; }

        PathInfo UI { get; }

        PathInfo Fonts { get; }

        PathInfo Icons { get; }

        PathInfo Models { get; }

        PathInfo Textures { get; }
    }
}
