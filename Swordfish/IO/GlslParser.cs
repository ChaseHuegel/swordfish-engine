using System.Text;
using Microsoft.Extensions.Logging;
using Swordfish.Graphics;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.IO;
using Shader = Swordfish.Graphics.Shader;
using SilkShaderType = Silk.NET.OpenGL.ShaderType;

namespace Swordfish.IO;

internal class GlslParser(in ILogger logger) : IFileParser<Shader>
{
    public string[] SupportedExtensions { get; } =
    [
        ".glsl",
    ];

    private readonly ILogger _logger = logger;

    object IFileParser.Parse(PathInfo file) => Parse(file);
    public Shader Parse(PathInfo file)
    {
        string? name = file.GetFileNameWithoutExtension();

        (string vertexSource, string fragmentSource) = ParseVertAndFrag(file);

        var vertex = new ShaderSource(name + ".vertex", vertexSource, Graphics.ShaderType.Vertex);
        var fragment = new ShaderSource(name + ".fragment", fragmentSource, Graphics.ShaderType.Fragment);

        return new Shader(name, vertex, fragment);
    }

    private (string vertexSource, string fragmentSource) ParseVertAndFrag(PathInfo file)
    {
        string shaderName = file.GetFileNameWithoutExtension();

        List<string> includedFiles = [];
        List<string> includedSources = [];

        //  Process the original source
        ProcessSource(file, out string? versionDirective, out string? source, ref includedFiles);

        if (source == null)
            throw new FormatException($"The shader '{shaderName}' was empty or failed to parse.");

        //  Recursively process all included sources
        while (includedFiles.Count > 0)
        {
            PathInfo includedFile = file.GetDirectory().At(includedFiles[0]);
            includedFiles.RemoveAt(0);

            ProcessSource(includedFile, out string? inheritedVersionDirective, out string? includedSource, ref includedFiles);

            versionDirective ??= inheritedVersionDirective;

            if (includedSource != null)
            {
                includedSources.Add(includedSource);
            }
            else
            {
                _logger.LogWarning("Shader '{shaderName}' includes '{includedFile}' that was empty or failed to parse.", shaderName, includedFile.GetFileNameWithoutExtension());
            }
        }

        //  Ensure we have a version
        if (versionDirective == null)
        {
            versionDirective = "#version 330 core";
            _logger.LogWarning("A #version directive was not found for shader '{shaderName}'; defaulting to {versionDirective}'.", shaderName, versionDirective);
        }

        //  The min required attributes for functionality
        StringBuilder attributesBuilder = new();
        attributesBuilder.AppendLine("#ifdef VERTEX");
        attributesBuilder.AppendLine("layout (location = 0) in vec3 in_position;");
        attributesBuilder.AppendLine("layout (location = 1) in vec4 in_color;");
        attributesBuilder.AppendLine("layout (location = 2) in vec3 in_uv;");
        attributesBuilder.AppendLine("layout (location = 3) in vec3 in_normal;");
        attributesBuilder.AppendLine("layout (location = 4) in mat4 model;");
        attributesBuilder.AppendLine("uniform mat4 view;");
        attributesBuilder.AppendLine("uniform mat4 projection;");
        attributesBuilder.AppendLine("out vec3 VertexPosition;");
        attributesBuilder.AppendLine("out vec4 VertexColor;");
        attributesBuilder.AppendLine("out vec3 TextureCoord;");
        attributesBuilder.AppendLine("out vec3 VertexNormal;");
        attributesBuilder.AppendLine("#endif");
        attributesBuilder.AppendLine();
        attributesBuilder.AppendLine("#ifdef FRAGMENT");
        attributesBuilder.AppendLine("in vec3 VertexPosition;");
        attributesBuilder.AppendLine("in vec4 VertexColor;");
        attributesBuilder.AppendLine("in vec3 TextureCoord;");
        attributesBuilder.AppendLine("in vec3 VertexNormal;");
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
    VertexNormal = in_normal;
    gl_Position = vertex();
#endif

#ifdef FRAGMENT
    FragColor = fragment();
    if (FragColor.a == 0)
        discard;
#endif
}";

        List<string> combinedSources =
        [
            versionDirective,
            attributes,
            /* includes go here */
            source,
            mainMethod,
        ];

        //  Includes are added in reverse order so deeper
        //  dependencies are available to their dependents.
        for (int i = includedSources.Count - 1; i >= 0; i--)
            combinedSources.Insert(2, includedSources[i]);

        combinedSources.Insert(1, $"#define {SilkShaderType.VertexShader.GetDirective()}");
        string vertexSource = string.Join(Environment.NewLine, combinedSources);

        combinedSources.RemoveAt(1);
        combinedSources.Insert(1, $"#define {SilkShaderType.FragmentShader.GetDirective()}");
        string fragmentSource = string.Join(Environment.NewLine, combinedSources);

        return (vertexSource, fragmentSource);
    }

    private static void ProcessSource(PathInfo file, out string? versionDirective, out string? source, ref List<string> includedFiles)
    {
        versionDirective = null;
        source = null;
        includedFiles ??= [];
        StringBuilder sourceBuilder = new();

        using (Stream stream = file.Open())
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
