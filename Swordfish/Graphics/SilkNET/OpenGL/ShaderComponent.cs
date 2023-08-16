using System.Runtime.CompilerServices;
using Silk.NET.OpenGL;
using Swordfish.Library.Diagnostics;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal sealed class ShaderComponent : ManagedHandle<uint>, IEquatable<ShaderComponent>
{
    public string Name { get; private set; }

    private readonly GL GL;
    private readonly Silk.NET.OpenGL.ShaderType Type;

    public ShaderComponent(GL gl, string name, Silk.NET.OpenGL.ShaderType type, string source)
    {
        GL = gl;
        Name = name;
        Type = type;

        GL.ShaderSource(Handle, source);
        GL.CompileShader(Handle);

        string shaderError = GL.GetShaderInfoLog(Handle);
        if (!string.IsNullOrWhiteSpace(shaderError))
            Debugger.Log($"Failed to compile {type} '{Name}'.\n{shaderError}", LogType.ERROR);
    }

    protected override uint CreateHandle()
    {
        return GL.CreateShader(Type);
    }

    protected override void OnDisposed()
    {
        GL.DeleteShader(Handle);
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
            return true;

        if (obj is not ShaderComponent other)
            return false;

        return Equals(other);
    }

    public override int GetHashCode()
    {
        return (int)Handle;
    }

    public override string? ToString()
    {
        return base.ToString() + $"[{Handle}]";
    }
}
