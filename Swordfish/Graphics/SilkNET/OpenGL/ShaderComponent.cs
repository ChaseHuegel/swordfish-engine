using System.Runtime.CompilerServices;
using Silk.NET.OpenGL;
using Swordfish.Library.Types;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal sealed class ShaderComponent : ManagedHandle<uint>, IEquatable<ShaderComponent>
{
    public string Name { get; }

    private readonly GL _gl;
    private readonly Silk.NET.OpenGL.ShaderType _type;

    public ShaderComponent(GL gl, string name, Silk.NET.OpenGL.ShaderType type, string source)
    {
        _gl = gl;
        Name = name;
        _type = type;
        Compile(source);
    }

    protected override uint CreateHandle()
    {
        return _gl.CreateShader(_type);
    }

    protected override void FreeHandle()
    {
        _gl.DeleteShader(Handle);
    }

    public void Compile(string source)
    {
        _gl.ShaderSource(Handle, source);
        _gl.CompileShader(Handle);

        string shaderError = _gl.GetShaderInfoLog(Handle);
        if (!string.IsNullOrWhiteSpace(shaderError))
        {
            //  TODO dont want to throw
            throw new GLException($"Failed to compile {_type} '{Name}'.\n{shaderError}");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ShaderComponent? other)
    {
        return Handle.Equals(other?.Handle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        return obj is ShaderComponent other && Equals(other);
    }

    public override int GetHashCode()
    {
        return (int)Handle;
    }

    public override string ToString()
    {
        return base.ToString() + $"[{Handle}]";
    }
}
