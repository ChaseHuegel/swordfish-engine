using Swordfish.Library.Annotations;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.IO;

namespace Swordfish.Graphics;

public class Texture : Handle
{
    public readonly string Name;

    internal readonly IPath Source;

    public Texture([NotNull] string name, [NotNull] IPath source)
    {
        Name = name;

        if (!source.FileExists())
            Debugger.Log($"No source provided for texture '{Name}'.", LogType.ERROR);

        Source = source;
    }

    protected override void OnDisposed()
    {
        //  Do nothing
    }
}
