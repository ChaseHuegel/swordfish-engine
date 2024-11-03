using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.OpenGL;
using Swordfish.Library.IO;

namespace Swordfish.Graphics.SilkNET.OpenGL.Renderers;

internal class GLLineRenderer : IRenderStage, ILineRenderer
{
    private ShaderProgram? ShaderProgram;
    private VertexArrayObject<float>? VAO;

    //  ! There will likely be lock contention issues later.
    private readonly object LinesLock = new();
    private readonly List<Line> Lines = [];
    private readonly List<int> LineVertexOffsets = [];
    private readonly List<uint> LineVertexCounts = [];
    private readonly List<float> LineVertexData = [];

    private readonly object NoDepthLinesLock = new();
    private readonly List<Line> NoDepthLines = [];
    private readonly List<int> NoDepthLineVertexOffsets = [];
    private readonly List<uint> NoDepthLineVertexCounts = [];
    private readonly List<float> NoDepthLineVertexData = [];

    private readonly GL GL;
    private readonly GLContext GLContext;
    private readonly IFileParseService _fileParseService;
    private readonly IPathService PathService;

    public GLLineRenderer(GL gl, GLContext glContext, IFileParseService fileParseService, IPathService pathService)
    {
        GL = gl;
        GLContext = glContext;
        _fileParseService = fileParseService;
        PathService = pathService;
    }

    public void Initialize(IRenderContext renderContext)
    {
        if (renderContext is not GLRenderContext)
        {
            throw new NotSupportedException($"{nameof(GLLineRenderer)} only supports an OpenGL {nameof(IRenderContext)}.");
        }

        Shader shader = _fileParseService.Parse<Shader>(PathService.Shaders.At("lines.glsl"));
        VAO = GLContext.CreateVertexArrayObject(Array.Empty<float>());

        VAO.Bind();
        VAO.VertexBufferObject.Bind();
        VAO.SetVertexAttribute(0, 3, VertexAttribPointerType.Float, 7, 0);
        VAO.SetVertexAttribute(1, 4, VertexAttribPointerType.Float, 7, 3);

        ShaderProgram = ShaderToShaderProgram(shader);
        ShaderProgram.BindAttributeLocation("in_position", 0);
        ShaderProgram.BindAttributeLocation("in_color", 1);
    }

    public void PreRender(double delta, Matrix4x4 view, Matrix4x4 projection)
    {
    }

    public int Render(double delta, Matrix4x4 view, Matrix4x4 projection)
    {
        ShaderProgram!.Activate();
        ShaderProgram.SetUniform("view", view);
        ShaderProgram.SetUniform("projection", projection);

        VAO!.Bind();

        int drawCalls = DrawLines(VAO, LinesLock, Lines, LineVertexOffsets, LineVertexCounts, LineVertexData, true);
        drawCalls += DrawLines(VAO, NoDepthLinesLock, NoDepthLines, NoDepthLineVertexOffsets, NoDepthLineVertexCounts, NoDepthLineVertexData, false);

        return drawCalls;
    }

    public Line CreateLine(bool alwaysOnTop = false)
    {
        return CreateLine(Vector3.Zero, Vector3.Zero, alwaysOnTop);
    }

    public Line CreateLine(Vector3 start, Vector3 end, bool alwaysOnTop = false)
    {
        return CreateLine(start, end, Vector4.One, alwaysOnTop);
    }

    public Line CreateLine(Vector3 start, Vector3 end, Vector4 color, bool alwaysOnTop = false)
    {
        if (alwaysOnTop)
        {
            return CreateLineInternal(NoDepthLinesLock, NoDepthLines, NoDepthLineVertexOffsets, NoDepthLineVertexCounts, NoDepthLineVertexData, start, end, color);
        }

        return CreateLineInternal(LinesLock, Lines, LineVertexOffsets, LineVertexCounts, LineVertexData, start, end, color);
    }

    public void DeleteLine(Line line)
    {
        if (TryDeleteLine(LinesLock, Lines, LineVertexOffsets, LineVertexCounts, LineVertexData, line))
        {
            return;
        }

        TryDeleteLine(NoDepthLinesLock, NoDepthLines, NoDepthLineVertexOffsets, NoDepthLineVertexCounts, NoDepthLineVertexData, line);
    }

    private Line CreateLineInternal(object lockObject, List<Line> lines, List<int> vertexOffsets, List<uint> vertexCounts, List<float> vertexData, Vector3 start, Vector3 end, Vector4 color)
    {
        var line = new Line(this, start, end, color);
        lock (lockObject)
        {
            vertexOffsets.Add(lines.Count * 2);
            vertexCounts.Add(2);

            vertexData.Capacity += 14;

            // start X,Y,Z
            vertexData.Add(0);
            vertexData.Add(0);
            vertexData.Add(0);

            // start r,g,b,a
            vertexData.Add(0);
            vertexData.Add(0);
            vertexData.Add(0);
            vertexData.Add(0);

            // end X,Y,Z
            vertexData.Add(0);
            vertexData.Add(0);
            vertexData.Add(0);

            // end r,g,b,a
            vertexData.Add(0);
            vertexData.Add(0);
            vertexData.Add(0);
            vertexData.Add(0);

            lines.Add(line);
        }

        return line;
    }

    private static bool TryDeleteLine(object lockObject, List<Line> lines, List<int> vertexOffsets, List<uint> vertexCounts, List<float> vertexData, Line line)
    {
        lock (lockObject)
        {
            int index = lines.IndexOf(line);
            if (index == -1)
            {
                return false;
            }

            vertexOffsets.RemoveAt(index);
            vertexCounts.RemoveAt(index);
            vertexData.RemoveRange(index, 14);
            lines.RemoveAt(index);
            return true;
        }
    }

    private ShaderProgram ShaderToShaderProgram(Shader shader)
    {
        ShaderComponent[] shaderComponents = shader.Sources.Select(ShaderSourceToShaderComponent).ToArray();
        return GLContext.CreateShaderProgram(shader.Name, shaderComponents);
    }

    private ShaderComponent ShaderSourceToShaderComponent(ShaderSource shaderSource)
    {
        return GLContext.CreateShaderComponent(shaderSource.Name, shaderSource.Type.ToSilkShaderType(), shaderSource.Source);
    }

    private int DrawLines(VertexArrayObject<float> vao, object lockObject, List<Line> lines, List<int> vertexOffsets, List<uint> vertexCounts, List<float> vertexData, bool depthTest)
    {
        lock (lockObject)
        {
            if (lines.Count == 0)
            {
                return 0;
            }

            for (int i = 0, n = 0; i < lines.Count; i++, n += 14)
            {
                Line line = lines[i];
                vertexData[n + 0] = line.Start.X;
                vertexData[n + 1] = line.Start.Y;
                vertexData[n + 2] = line.Start.Z;

                vertexData[n + 3] = line.Color.X;
                vertexData[n + 4] = line.Color.Y;
                vertexData[n + 5] = line.Color.Z;
                vertexData[n + 6] = line.Color.W;

                vertexData[n + 7] = line.End.X;
                vertexData[n + 8] = line.End.Y;
                vertexData[n + 9] = line.End.Z;

                vertexData[n + 10] = line.Color.X;
                vertexData[n + 11] = line.Color.Y;
                vertexData[n + 12] = line.Color.Z;
                vertexData[n + 13] = line.Color.W;
            }

            vao.VertexBufferObject.UpdateData(CollectionsMarshal.AsSpan(vertexData));
            GL.Set(EnableCap.DepthTest, depthTest);
            GL.MultiDrawArrays(PrimitiveType.Lines, CollectionsMarshal.AsSpan(vertexOffsets), CollectionsMarshal.AsSpan(vertexCounts), (uint)lines.Count);
        }

        return 1;
    }
}