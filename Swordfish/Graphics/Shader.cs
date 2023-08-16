using System.Diagnostics.CodeAnalysis;
using Swordfish.Library.Diagnostics;

namespace Swordfish.Graphics;

public sealed class Shader : Handle
{
    public readonly string Name;
    public readonly ShaderSource[] Sources;

    public Shader([NotNull] string name, params ShaderSource[] sources)
    {
        Name = name;
        Sources = sources;
    }

    public bool TryGetSource(ShaderType type, out ShaderSource? source)
    {
        for (int i = 0; i < Sources.Length; i++)
        {
            var shaderSource = Sources[i];
            if (shaderSource.Type == type)
            {
                source = shaderSource;
                return true;
            }
        }

        source = null;
        return false;
    }

    protected override void OnDisposed()
    {
        //  Do nothing
    }
}
