using System.Diagnostics.CodeAnalysis;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.IO;

namespace Swordfish.Graphics.SilkNET;

public sealed class Shader : IDisposable
{
    public readonly string Name;

    internal volatile bool Disposed;
    internal readonly IPath Source;

    internal IHandle? Handle;

    public Shader([NotNull] string name, [NotNull] IPath source)
    {
        Name = name;

        if (!source.FileExists())
            Debugger.Log($"No source provided for shader '{Name}'.", LogType.ERROR);

        Source = source;
    }

    public void Dispose()
    {
        if (Disposed)
        {
            Debugger.Log($"Attempted to dispose {this} but it is already disposed.", LogType.WARNING);
            return;
        }

        Disposed = true;
    }
}
