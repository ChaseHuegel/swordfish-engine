using System.Numerics;
using System.Runtime.CompilerServices;
using Silk.NET.OpenGL;
using Swordfish.Library.Diagnostics;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal sealed class ShaderProgram : GLHandle, IEquatable<ShaderProgram>
{
    public string Name { get; private set; }

    private readonly GL GL;
    private readonly Dictionary<string, int> UniformLocations;

    public ShaderProgram(GL gl, string name, ShaderComponent[] components)
    {
        GL = gl;
        Name = name;
        UniformLocations = new Dictionary<string, int>();

        for (int i = 0; i < components.Length; i++)
            GL.AttachShader(Handle, components[i].Handle);

        GL.LinkProgram(Handle);

        for (int i = 0; i < components.Length; i++)
            GL.DetachShader(Handle, components[i].Handle);

        GL.GetProgram(Handle, GLEnum.LinkStatus, out int status);
        if (status != 0)
        {
            GL.GetProgram(Handle, GLEnum.ActiveUniforms, out int uniformCount);

            for (uint i = 0; i < uniformCount; i++)
            {
                string key = GL.GetActiveUniform(Handle, i, out _, out _);
                int location = GL.GetUniformLocation(Handle, key);
                UniformLocations.Add(key, location);
            }

            Debugger.Log($"Created shader '{Name}'.\n\tUniforms: [{string.Join(", ", UniformLocations.Keys)}]");
        }
        else
        {
            Debugger.Log($"Failed to link program for shader '{Name}'.\n{GL.GetProgramInfoLog(Handle)}", LogType.ERROR);
        }
    }

    protected override uint CreateHandle()
    {
        return GL.CreateProgram();
    }

    protected override void FreeHandle()
    {
        GL.DeleteProgram(Handle);
    }

    protected override void BindHandle()
    {
        GL.UseProgram(Handle);
    }

    protected override void UnbindHandle()
    {
        GL.UseProgram(0);
    }

    public void Activate()
    {
        Bind();
    }

    public uint BindAttributeLocation(string attribute, uint location)
    {
        GL.BindAttribLocation(Handle, location, attribute);
        return location;
    }

    public uint GetAttributeLocation(string attribute)
    {
        int location = GL.GetAttribLocation(Handle, attribute);

        if (location < 0)
            Debugger.Log($"Shader attribute '{attribute}' not found in shader '{Name}'.", LogType.ERROR);

        return (uint)location;
    }

    public bool HasUniform(string uniform)
    {
        return TryGetUniform(uniform, out _);
    }

    public void SetUniform(string uniform, int value)
    {
        if (TryGetUniform(uniform, out int location))
            GL.Uniform1(location, value);
    }

    public void SetUniform(string uniform, float value)
    {
        if (TryGetUniform(uniform, out int location))
            GL.Uniform1(location, value);
    }

    public void SetUniform(string uniform, Vector2 value)
    {
        if (TryGetUniform(uniform, out int location))
            GL.Uniform2(location, value.X, value.Y);
    }

    public void SetUniform(string uniform, Vector3 value)
    {
        if (TryGetUniform(uniform, out int location))
            GL.Uniform3(location, value.X, value.Y, value.Z);
    }

    public void SetUniform(string uniform, Vector4 value)
    {
        if (TryGetUniform(uniform, out int location))
            GL.Uniform4(location, value.X, value.Y, value.Z, value.W);
    }

    public unsafe void SetUniform(string uniform, Matrix4x4 value)
    {
        if (TryGetUniform(uniform, out int location))
            GL.UniformMatrix4(location, 1, false, (float*)&value);
    }

    private bool TryGetUniform(string uniform, out int location)
    {
        if (!UniformLocations.TryGetValue(uniform, out location))
        {
            location = GL.GetUniformLocation(Handle, uniform);
            UniformLocations.Add(uniform, location);
        }

        if (location == -1)
            Debugger.Log($"Uniform '{uniform}' not found in the shader '{Name}'.", LogType.WARNING);

        return location != -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ShaderProgram? other)
    {
        return Handle.Equals(other?.Handle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj))
            return true;

        if (obj is not ShaderProgram other)
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
