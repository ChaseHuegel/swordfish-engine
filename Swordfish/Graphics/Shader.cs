using System.Diagnostics.CodeAnalysis;
using Swordfish.Library.Diagnostics;

namespace Swordfish.Graphics.SilkNET;

public sealed class Shader : IDisposable
{
    public readonly string Name;

    internal volatile bool Disposed;
    internal volatile bool Dirty;
    internal readonly Stream Source;

    internal IHandle? Handle;

    public Shader([NotNull] string name, [NotNull] Stream source)
    {
        Name = name;

        if (source.Length == 0)
            Debugger.Log($"No source provided for shader '{Name}'.", LogType.ERROR);

        Source = source;
        Dirty = true;
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
