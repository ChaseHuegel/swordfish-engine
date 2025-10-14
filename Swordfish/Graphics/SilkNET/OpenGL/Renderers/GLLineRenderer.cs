using System.Numerics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Silk.NET.OpenGL;
using Swordfish.Graphics.SilkNET.OpenGL.Util;
using Swordfish.IO;
using Swordfish.Library.IO;

namespace Swordfish.Graphics.SilkNET.OpenGL.Renderers;

// ReSharper disable once ClassNeverInstantiated.Global
internal sealed class GLLineRenderer(
    in ILogger logger,
    in GL gl,
    in GLContext glContext,
    in IFileParseService fileParseService,
    in VirtualFileSystem vfs)
    : IForwardRenderStage, ILineRenderer
{
    private ShaderProgram? _shaderProgram;
    private VertexArrayObject<float>? _vao;

    //  ! There will likely be lock contention issues later.
    private readonly object _linesLock = new();
    private readonly List<Line> _lines = [];
    private readonly List<int> _lineVertexOffsets = [];
    private readonly List<uint> _lineVertexCounts = [];
    private readonly List<float> _lineVertexData = [];

    private readonly object _noDepthLinesLock = new();
    private readonly List<Line> _noDepthLines = [];
    private readonly List<int> _noDepthLineVertexOffsets = [];
    private readonly List<uint> _noDepthLineVertexCounts = [];
    private readonly List<float> _noDepthLineVertexData = [];

    private readonly ILogger _logger = logger;
    private readonly GL _gl = gl;
    private readonly GLContext _glContext = glContext;
    private readonly IFileParseService _fileParseService = fileParseService;
    private readonly VirtualFileSystem _vfs = vfs;

    public void Initialize(IRenderContext renderContext)
    {
        if (renderContext is not GLRenderContext)
        {
            throw new NotSupportedException($"{nameof(GLLineRenderer)} only supports an OpenGL {nameof(IRenderContext)}.");
        }

        if (!_vfs.TryGetFile(AssetPaths.Shaders.At("lines.glsl"), out PathInfo linesShaderFile))
        {
            _logger.LogError("The shader source for OpenGL lines was not found. OpenGL lines will not be rendered.");
            return;
        }

        if (!_fileParseService.TryParse(linesShaderFile, out Shader shader))
        {
            _logger.LogError("Failed to parse the OpenGL lines shader. OpenGL lines will not be rendered.");
            return;
        }
        
        _vao = _glContext.CreateVertexArrayObject(Array.Empty<float>());

        _vao.Bind();
        _vao.VertexBufferObject.Bind();
        _vao.SetVertexAttribute(0, 3, VertexAttribPointerType.Float, 7, 0);
        _vao.SetVertexAttribute(1, 4, VertexAttribPointerType.Float, 7, 3);

        _shaderProgram = shader.CreateProgram(_glContext);
        _shaderProgram.BindAttributeLocation("in_position", 0);
        _shaderProgram.BindAttributeLocation("in_color", 1);
    }

    public void PreRender(double delta, Matrix4x4 view, Matrix4x4 projection)
    {
    }

    public int Render(double delta, Matrix4x4 view, Matrix4x4 projection)
    {
        if (_shaderProgram == null || _vao == null)
        {
            return 0;
        }
        
        _shaderProgram.Activate();
        _shaderProgram.SetUniform("view", view);
        _shaderProgram.SetUniform("projection", projection);

        _vao.Bind();

        int drawCalls = DrawLines(_vao, _linesLock, _lines, _lineVertexOffsets, _lineVertexCounts, _lineVertexData, true);
        drawCalls += DrawLines(_vao, _noDepthLinesLock, _noDepthLines, _noDepthLineVertexOffsets, _noDepthLineVertexCounts, _noDepthLineVertexData, false);

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
            return CreateLineInternal(_noDepthLinesLock, _noDepthLines, _noDepthLineVertexOffsets, _noDepthLineVertexCounts, _noDepthLineVertexData, start, end, color);
        }

        return CreateLineInternal(_linesLock, _lines, _lineVertexOffsets, _lineVertexCounts, _lineVertexData, start, end, color);
    }

    public void DeleteLine(Line line)
    {
        if (TryDeleteLine(_linesLock, _lines, _lineVertexOffsets, _lineVertexCounts, _lineVertexData, line))
        {
            return;
        }

        TryDeleteLine(_noDepthLinesLock, _noDepthLines, _noDepthLineVertexOffsets, _noDepthLineVertexCounts, _noDepthLineVertexData, line);
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
            _gl.Set(EnableCap.DepthTest, depthTest);
            _gl.MultiDrawArrays(PrimitiveType.Lines, CollectionsMarshal.AsSpan(vertexOffsets), CollectionsMarshal.AsSpan(vertexCounts), (uint)lines.Count);
        }

        return 1;
    }
}