using System.Numerics;
using Ninject;
using Silk.NET.OpenGL;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.IO;
using Path = Swordfish.Library.IO.Path;

namespace Swordfish.Graphics.SilkNET;

public sealed class Shader : IDisposable
{
    public const string VertexFileExtension = ".vert";
    public const string FragmentFileExtension = ".frag";

    private GL GL => gl ??= SwordfishEngine.Kernel.Get<GL>();
    private GL gl;

    public readonly string Name;

    private readonly uint Handle;
    private readonly Dictionary<string, int> UniformLocations = new();

    private volatile bool Disposed;

    public Shader(string name, string vertexSource, string fragmentSource)
    {
        Name = name;

        if (string.IsNullOrWhiteSpace(vertexSource))
            Debugger.Log($"No vertex source provided for shader '{Name}'.", LogType.ERROR);

        if (string.IsNullOrWhiteSpace(fragmentSource))
            Debugger.Log($"No fragment source provided for shader '{Name}'.", LogType.ERROR);

        uint vertexHandle = CreateShaderHandle(ShaderType.VertexShader, vertexSource);
        uint fragmentHandle = CreateShaderHandle(ShaderType.FragmentShader, fragmentSource);

        Handle = GL!.CreateProgram();
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

    public void Dispose()
    {
        if (Disposed)
        {
            Debugger.Log($"Attempted to dispose {this} but it is already disposed.", LogType.WARNING);
            return;
        }

        Disposed = true;
        GL.DeleteProgram(Handle);
    }

    public void Use()
    {
        if (Disposed)
        {
            Debugger.Log($"Attempted to use shader '{Name}' but it is disposed.", LogType.ERROR);
            return;
        }

        GL.UseProgram(Handle);
    }

    public bool HasUniform(string name)
    {
        return TryGetUniform(name, out _);
    }

    public void SetUniform(string name, int value)
    {
        if (TryGetUniform(name, out int location))
            GL.Uniform1(location, value);
    }

    public void SetUniform(string name, float value)
    {
        if (TryGetUniform(name, out int location))
            GL.Uniform1(location, value);
    }

    public void SetUniform(string name, Vector2 value)
    {
        if (TryGetUniform(name, out int location))
            GL.Uniform2(location, value.X, value.Y);
    }

    public void SetUniform(string name, Vector3 value)
    {
        if (TryGetUniform(name, out int location))
            GL.Uniform3(location, value.X, value.Y, value.Z);
    }

    public void SetUniform(string name, Vector4 value)
    {
        if (TryGetUniform(name, out int location))
            GL.Uniform4(location, value.X, value.Y, value.Z, value.W);
    }

    private bool TryGetUniform(string name, out int location)
    {
        if (!UniformLocations.TryGetValue(name, out location))
        {
            location = GL.GetUniformLocation(Handle, name);
            UniformLocations.Add(name, location);
        }

        if (location == -1)
            Debugger.Log($"Uniform '{name}' does not exist in the shader '{Name}'.", LogType.WARNING);

        return location != -1;
    }

    private uint CreateShaderHandle(ShaderType shaderType, string source)
    {
        uint handle = GL.CreateShader(shaderType);
        GL.ShaderSource(handle, source);
        GL.CompileShader(handle);

        string shaderError = GL.GetShaderInfoLog(handle);
        if (!string.IsNullOrWhiteSpace(shaderError))
            Debugger.Log($"Failed to compile {shaderType} '{Name}'.\n{shaderError}", LogType.ERROR);

        return handle;
    }

    public static Shader LoadFrom(IPath path)
    {
        IFileService fileService = SwordfishEngine.Kernel.Get<IFileService>();

        string sourceName = path.GetFileNameWithoutExtension();
        IPath sourceDirectory = new Path(path.GetDirectoryName());
        string vertexFile = sourceName + VertexFileExtension;
        string fragmentFile = sourceName + FragmentFileExtension;

        IPath vertexPath = sourceDirectory.At(vertexFile);
        IPath fragmentPath = sourceDirectory.At(fragmentFile);

        string vertexSource, fragmentSource;

        using (Stream stream = fileService.Read(vertexPath))
        using (StreamReader reader = new(stream))
        {
            vertexSource = reader.ReadToEnd();
        }

        using (Stream stream = fileService.Read(fragmentPath))
        using (StreamReader reader = new(stream))
        {
            fragmentSource = reader.ReadToEnd();
        }

        return new Shader(sourceName, vertexSource, fragmentSource);
    }
}
