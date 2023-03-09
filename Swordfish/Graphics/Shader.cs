using System.Diagnostics.CodeAnalysis;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.IO;

namespace Swordfish.Graphics.SilkNET;

public sealed class Shader : Handle
{
    public readonly string Name;

    internal readonly IPath Source;

    public Shader([NotNull] string name, [NotNull] IPath source)
    {
        Name = name;

        if (!source.FileExists())
            Debugger.Log($"No source provided for shader '{Name}'.", LogType.ERROR);

        Source = source;
    }

    protected override void OnDisposed()
    {
        //  Do nothing
    }
}
