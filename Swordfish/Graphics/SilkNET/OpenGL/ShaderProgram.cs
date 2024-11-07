using System.Numerics;
using System.Runtime.CompilerServices;
using Silk.NET.OpenGL;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal sealed class ShaderProgram : GLHandle, IEquatable<ShaderProgram>
{
    public string Name { get; }

    private readonly GL _gl;
    private readonly Dictionary<string, int> _uniformLocations;

    public ShaderProgram(GL gl, string name, ShaderComponent[] components)
    {
        _gl = gl;
        Name = name;
        _uniformLocations = new Dictionary<string, int>();

        for (var i = 0; i < components.Length; i++)
        {
            _gl.AttachShader(Handle, components[i].Handle);
        }

        _gl.LinkProgram(Handle);

        for (var i = 0; i < components.Length; i++)
        {
            _gl.DetachShader(Handle, components[i].Handle);
        }

        _gl.GetProgram(Handle, GLEnum.LinkStatus, out int status);
        if (status != 0)
        {
            _gl.GetProgram(Handle, GLEnum.ActiveUniforms, out int uniformCount);

            for (uint i = 0; i < uniformCount; i++)
            {
                string key = _gl.GetActiveUniform(Handle, i, out _, out _);
                int location = _gl.GetUniformLocation(Handle, key);
                _uniformLocations.Add(key, location);
            }
        }
        else
        {
            //  TODO dont want to throw
            throw new GLException($"Failed to link program for shader '{Name}'.\n{_gl.GetProgramInfoLog(Handle)}");
        }
    }

    protected override uint CreateHandle()
    {
        return _gl.CreateProgram();
    }

    protected override void FreeHandle()
    {
        _gl.DeleteProgram(Handle);
    }

    protected override void BindHandle()
    {
        _gl.UseProgram(Handle);
    }

    protected override void UnbindHandle()
    {
        _gl.UseProgram(0);
    }

    public void Activate()
    {
        Bind();
    }

    public void BindAttributeLocation(string attribute, uint location)
    {
        _gl.BindAttribLocation(Handle, location, attribute);
    }

    public uint GetAttributeLocation(string attribute)
    {
        int location = _gl.GetAttribLocation(Handle, attribute);

        if (location < 0)
        {
            //  TODO dont want to throw here
            throw new GLException($"Shader attribute '{attribute}' not found in shader '{Name}'.");
        }

        return (uint)location;
    }

    public bool HasUniform(string uniform)
    {
        return TryGetUniform(uniform, out _);
    }

    public void SetUniform(string uniform, int value)
    {
        if (TryGetUniform(uniform, out int location))
        {
            _gl.Uniform1(location, value);
        }
    }

    public void SetUniform(string uniform, float value)
    {
        if (TryGetUniform(uniform, out int location))
        {
            _gl.Uniform1(location, value);
        }
    }

    public void SetUniform(string uniform, Vector2 value)
    {
        if (TryGetUniform(uniform, out int location))
        {
            _gl.Uniform2(location, value.X, value.Y);
        }
    }

    public void SetUniform(string uniform, Vector3 value)
    {
        if (TryGetUniform(uniform, out int location))
        {
            _gl.Uniform3(location, value.X, value.Y, value.Z);
        }
    }

    public void SetUniform(string uniform, Vector4 value)
    {
        if (TryGetUniform(uniform, out int location))
        {
            _gl.Uniform4(location, value.X, value.Y, value.Z, value.W);
        }
    }

    public unsafe void SetUniform(string uniform, Matrix4x4 value)
    {
        if (TryGetUniform(uniform, out int location))
        {
            _gl.UniformMatrix4(location, 1, false, (float*)&value);
        }
    }

    private bool TryGetUniform(string uniform, out int location)
    {
        if (!_uniformLocations.TryGetValue(uniform, out location))
        {
            location = _gl.GetUniformLocation(Handle, uniform);
            _uniformLocations.Add(uniform, location);
        }

        if (location == -1)
        {
            //  TODO dont want to throw here
            throw new GLException($"Uniform '{uniform}' not found in the shader '{Name}'.");
        }

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
        {
            return true;
        }

        return obj is ShaderProgram other && Equals(other);
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
