using System.Numerics;
using System.Runtime.CompilerServices;
using Silk.NET.OpenGL;
using Swordfish.Library.Diagnostics;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal sealed class ShaderProgram : ManagedHandle<uint>, IEquatable<ShaderProgram>
{
    public string Name { get; private set; }

    private readonly GL GL;
    private readonly Dictionary<string, int> UniformLocations;

    public ShaderProgram(GL gl, string name, string vertexSource, string fragmentSource)
    {
        GL = gl;
        Name = name;
        UniformLocations = new Dictionary<string, int>();

        if (vertexSource.Length == 0)
            Debugger.Log($"No vertex source provided for shader '{Name}'.", LogType.ERROR);

        if (fragmentSource.Length == 0)
            Debugger.Log($"No fragment source provided for shader '{Name}'.", LogType.ERROR);

        //  TODO instead of hardcoding vert/frag requirements these should be extracted into a Shader type
        uint vertexHandle = CreateShaderHandle(ShaderType.VertexShader, vertexSource);
        uint fragmentHandle = CreateShaderHandle(ShaderType.FragmentShader, fragmentSource);

        GL.AttachShader(Handle, vertexHandle);
        GL.AttachShader(Handle, fragmentHandle);

        GL.LinkProgram(Handle);

        GL.DetachShader(Handle, vertexHandle);
        GL.DetachShader(Handle, fragmentHandle);
        GL.DeleteShader(vertexHandle);
        GL.DeleteShader(fragmentHandle);

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

    protected override void OnDisposed()
    {
        GL.DeleteProgram(Handle);
    }

    public void Use()
    {
        if (IsDisposed)
        {
            Debugger.Log($"Attempted to use shader '{Name}' but it is disposed.", LogType.ERROR);
            return;
        }

        GL.UseProgram(Handle);
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

    private unsafe uint CreateShaderHandle(ShaderType shaderType, string source)
    {
        uint handle = GL.CreateShader(shaderType);
        GL.ShaderSource(handle, source);
        GL.CompileShader(handle);

        string shaderError = GL.GetShaderInfoLog(handle);
        if (!string.IsNullOrWhiteSpace(shaderError))
            Debugger.Log($"Failed to compile {shaderType} '{Name}'.\n{shaderError}", LogType.ERROR);

        return handle;
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
