using System.Diagnostics.CodeAnalysis;

namespace Swordfish.Graphics;

public sealed class ShaderSource : Handle
{
    public readonly string Name;
    public readonly string Source;
    public readonly ShaderType Type;

    public ShaderSource([NotNull] string name, [NotNull] string source, ShaderType type)
    {
        Name = name;
        Source = source;
        Type = type;
    }

    protected override void OnDisposed()
    {
        //  Do nothing
    }
}
