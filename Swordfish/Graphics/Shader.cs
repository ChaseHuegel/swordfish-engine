using System.Diagnostics.CodeAnalysis;
using Swordfish.Library.Diagnostics;

namespace Swordfish.Graphics;

public sealed class Shader : Handle
{
    public readonly string Name;

    //  TODO introduce a ref type instead of copying shader source code everywhere
    internal readonly string[] Sources;

    public Shader([NotNull] string name, [NotNull] string[] sources)
    {
        Name = name;
        Sources = sources;
    }

    protected override void OnDisposed()
    {
        //  Do nothing
    }
}
