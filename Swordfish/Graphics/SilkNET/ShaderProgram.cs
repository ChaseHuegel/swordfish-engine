using System.Numerics;
using System.Text;
using Silk.NET.OpenGL;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.IO;

namespace Swordfish.Graphics.SilkNET;

public sealed class ShaderProgram : IDisposable
{
    private GL GL => gl ??= SwordfishEngine.Kernel.Get<GL>();
    private GL gl;

    public string Name { get; private set; }

    private uint Handle;
    private Dictionary<string, int> UniformLocations = new();

    private volatile bool Disposed;

    public ShaderProgram(string name, string[] sources)
        : this(name, sources, sources) { }

    public ShaderProgram(string name, string vertexSource, string fragmentSource)
        : this(name, new string[] { vertexSource }, new string[] { fragmentSource }) { }

    public ShaderProgram(string name, string[] vertexSources, string[] fragmentSources)
    {
        Name = name;

        if (vertexSources.Length == 0)
            Debugger.Log($"No vertex source provided for shader '{Name}'.", LogType.ERROR);

        if (fragmentSources.Length == 0)
            Debugger.Log($"No fragment source provided for shader '{Name}'.", LogType.ERROR);

        uint vertexHandle = CreateShaderHandle(ShaderType.VertexShader, vertexSources);
        uint fragmentHandle = CreateShaderHandle(ShaderType.FragmentShader, fragmentSources);

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

    public uint GetAttributeLocation(string name)
    {
        return (uint)GL.GetAttribLocation(Handle, name);
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

    public unsafe void SetUniform(string name, Matrix4x4 value)
    {
        if (TryGetUniform(name, out int location))
            GL.UniformMatrix4(location, 1, false, (float*)&value);
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

    private unsafe uint CreateShaderHandle(ShaderType shaderType, params string[] sources)
    {
        string source = string.Join(Environment.NewLine, sources);
        //  The define should always be on ln 2, assuming ln 1 is #version
        source = source.Insert(source.IndexOf("\n") + 1, $"#define {shaderType.GetDirective()}{Environment.NewLine}");

        uint handle = GL.CreateShader(shaderType);
        GL.ShaderSource(handle, source);
        GL.CompileShader(handle);

        string shaderError = GL.GetShaderInfoLog(handle);
        if (!string.IsNullOrWhiteSpace(shaderError))
            Debugger.Log($"Failed to compile {shaderType} '{Name}'.\n{shaderError}", LogType.ERROR);

        return handle;
    }

    public static ShaderProgram LoadFrom(IPath file)
    {
        return SwordfishEngine.WaitForMainThread(LoadFromWorker, file);
    }

    private static ShaderProgram LoadFromWorker(IPath file)
    {
        string shaderName = file.GetFileNameWithoutExtension();

        List<string> includedFiles = new();
        List<string> includedSources = new();

        //  Process the original source
        ProcessSourceFile(file, out string? versionDirective, out string? source, ref includedFiles);

        if (source == null)
            throw new FormatException($"The shader '{shaderName}' was empty or failed to parse.");

        //  Recursively process all included sources
        while (includedFiles.Count > 0)
        {
            IPath includedFile = file.GetDirectory().At(includedFiles[0]);
            includedFiles.RemoveAt(0);

            ProcessSourceFile(includedFile, out string? inheritedVersionDirective, out string? includedSource, ref includedFiles);

            versionDirective ??= inheritedVersionDirective;

            if (includedSource != null)
                includedSources.Add(includedSource);
            else
                Debugger.Log($"Shader '{shaderName}' includes '{includedFile.GetFileNameWithoutExtension}' that was empty or failed to parse.", LogType.WARNING);
        }

        //  Ensure we have a version
        if (versionDirective == null)
        {
            versionDirective = "#version 330 core";
            Debugger.Log($"A #version directive was not found for shader '{shaderName}'; defaulting to {versionDirective}'.", LogType.WARNING);
        }

        //  The min required attributes for functionality
        StringBuilder attributesBuilder = new();
        attributesBuilder.AppendLine("#ifdef VERTEX");
        attributesBuilder.AppendLine("in vec3 in_position;");
        attributesBuilder.AppendLine("in vec4 in_color;");
        attributesBuilder.AppendLine("in vec3 in_uv;");
        attributesBuilder.AppendLine("out vec3 VertexPosition;");
        attributesBuilder.AppendLine("out vec4 VertexColor;");
        attributesBuilder.AppendLine("out vec3 TextureCoord;");
        attributesBuilder.AppendLine("#endif");
        attributesBuilder.AppendLine();
        attributesBuilder.AppendLine("#ifdef FRAGMENT");
        attributesBuilder.AppendLine("in vec3 VertexPosition;");
        attributesBuilder.AppendLine("in vec4 VertexColor;");
        attributesBuilder.AppendLine("in vec3 TextureCoord;");
        attributesBuilder.AppendLine("out vec4 FragColor;");
        attributesBuilder.AppendLine("#endif");
        string attributes = attributesBuilder.ToString();

        //  The entry point for the shader
        const string mainMethod = @"
void main()
{
#ifdef VERTEX
    VertexPosition = in_position;
    VertexColor = in_color;
    TextureCoord = in_uv;
    gl_Position = vertex();
#endif

#ifdef FRAGMENT
    FragColor = fragment();
    if (FragColor.a == 0 || FragColor.rgb == vec3(0, 0, 0))
        discard;
#endif
}";

        List<string> combinedSources = new()
        {
            versionDirective,
            attributes,
            /* includes go here */
            source,
            mainMethod
        };

        //  Includes are added in reverse order so deeper
        //  dependencies are available to their dependents.
        for (int i = includedSources.Count - 1; i >= 0; i--)
            combinedSources.Insert(2, includedSources[i]);

        return new ShaderProgram(shaderName, combinedSources.ToArray());
    }

    private static void ProcessSourceFile(IPath file, out string? versionDirective, out string? source, ref List<string> includedFiles)
    {
        versionDirective = null;
        source = null;
        includedFiles ??= new();
        StringBuilder sourceBuilder = new();

        IFileService fileService = SwordfishEngine.Kernel.Get<IFileService>();
        using (Stream stream = fileService.Read(file))
        using (StreamReader reader = new(stream))
        {
            string? line = reader.ReadLine()?.Trim();

            if (line == null)
                return;

            if (line.StartsWith("#version", StringComparison.OrdinalIgnoreCase))
            {
                versionDirective = line;
                line = reader.ReadLine()?.Trim();
            }

            bool inDefaultMethod = false;
            int openBraces = 0, closeBraces = 0;
            while (line != null)
            {
                if (line.StartsWith("inout", StringComparison.OrdinalIgnoreCase))
                {
                    sourceBuilder.AppendLine("#ifdef " + GLExtensions.VERTEX_DIRECTIVE);
                    sourceBuilder.AppendLine(line.Replace("inout", "out"));
                    sourceBuilder.AppendLine("#endif");
                    sourceBuilder.AppendLine();
                    sourceBuilder.AppendLine("#ifdef " + GLExtensions.FRAGMENT_DIRECTIVE);
                    sourceBuilder.AppendLine(line.Replace("inout", "in"));
                    sourceBuilder.AppendLine("#endif");
                }
                else if (line.StartsWith("vec4 vertex"))
                {
                    sourceBuilder.AppendLine("#ifdef " + GLExtensions.VERTEX_DIRECTIVE);
                    sourceBuilder.AppendLine(line);
                    inDefaultMethod = true;
                }
                else if (line.StartsWith("vec4 fragment"))
                {
                    sourceBuilder.AppendLine("#ifdef " + GLExtensions.FRAGMENT_DIRECTIVE);
                    sourceBuilder.AppendLine(line);
                    inDefaultMethod = true;
                }
                else if (line.StartsWith("#include"))
                {
                    string includedFile = line["#include".Length..].Trim().Trim(' ', '\t', '"', '\'', '<', '>');
                    includedFiles.Add(includedFile);
                }
                else
                {
                    sourceBuilder.AppendLine(line);
                }

                if (line.Contains('{'))
                    openBraces++;

                if (line.Contains('}'))
                    closeBraces++;

                if (inDefaultMethod && openBraces > 0 && closeBraces > 0 && openBraces == closeBraces)
                {
                    openBraces = 0;
                    closeBraces = 0;
                    inDefaultMethod = false;
                    sourceBuilder.AppendLine("#endif");
                }

                line = reader.ReadLine()?.Trim();
            }
        }

        source = sourceBuilder.ToString();
    }
}
