using System.Numerics;
using Silk.NET.OpenGL;
using Swordfish.Settings;
using Swordfish.Types;

namespace Swordfish.Graphics.SilkNET.OpenGL.Renderers;

// ReSharper disable once ClassNeverInstantiated.Global
internal sealed class GLScreenSpaceRenderer : IScreenSpaceRenderStage
{
    private const int VERTEX_STRIDE = 10;
    private const int VERTICES_PER_QUAD = 4;

    private readonly GL _gl;
    private readonly RenderSettings _renderSettings;
    private readonly VertexArrayObject<float>? _vao;

    //  One buffer per frame. Consecutive rects that share materials stay in one run.
    private readonly List<GLMaterial[]> _runMaterials = [];
    private readonly List<GLMaterial.Scope> _materialScopes = [];
    private readonly Action<GLRectRenderTarget> _appendTarget;

    private float[] _vertices = [];
    private int _vertexCount;

    private int[] _quadOffsets = [];
    private uint[] _quadCounts = [];
    private int _quadCount;

    private int[] _runQuadOffsets = [];
    private int[] _runQuadCounts = [];
    private int _runCount;

    private GLMaterial[]? _activeMaterials;
    private int _runQuadOffset;

    public GLScreenSpaceRenderer(in GL gl, in GLContext glContext, in RenderSettings renderSettings)
    {
        _gl = gl;
        _renderSettings = renderSettings;
        _appendTarget = AppendTarget;

        _vao = glContext.CreateVertexArrayObject(Array.Empty<float>());
        _vao.Bind();
        _vao.VertexBufferObject.Bind();
        _vao.SetVertexAttribute(0, 3, VertexAttribPointerType.Float, VERTEX_STRIDE, 0);
        _vao.SetVertexAttribute(1, 4, VertexAttribPointerType.Float, VERTEX_STRIDE, 3);
        _vao.SetVertexAttribute(2, 3, VertexAttribPointerType.Float, VERTEX_STRIDE, 7);
        _vao.VertexBufferObject.Unbind();
        _vao.Unbind();
    }

    public void PreRender(double delta, RenderScene renderScene, bool isDepthPass)
    {
        if (isDepthPass)
        {
            return;
        }

        _vertexCount = 0;
        _quadCount = 0;
        _runCount = 0;
        _runQuadOffset = 0;
        _activeMaterials = null;
        _runMaterials.Clear();

        renderScene.RectRenderTargets.ForEach(_appendTarget);
        CloseRun();
    }

    public int Render(double delta, RenderScene renderScene, Action<ShaderProgram> shaderActivationCallback, bool isDepthPass)
    {
        if (_vao == null || isDepthPass || _runCount == 0)
        {
            return 0;
        }

        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _gl.Set(EnableCap.DepthTest, false);
        _gl.PolygonMode(TriangleFace.FrontAndBack, _renderSettings.Wireframe ? PolygonMode.Line : PolygonMode.Fill);

        _vao.Bind();
        _vao.VertexBufferObject.UpdateStreamingData(_vertices.AsSpan(0, _vertexCount));

        for (var i = 0; i < _runCount; i++)
        {
            GLMaterial[] materials = _runMaterials[i];

            //  The scopes must outlive the draw, so they are collected and released after it.
            _materialScopes.Clear();
            for (var n = 0; n < materials.Length; n++)
            {
                _materialScopes.Add(materials[n].Use());
                shaderActivationCallback(materials[n].ShaderProgram);
            }

            _gl.MultiDrawArrays(
                PrimitiveType.TriangleFan,
                _quadOffsets.AsSpan(_runQuadOffsets[i], _runQuadCounts[i]),
                _quadCounts.AsSpan(_runQuadOffsets[i], _runQuadCounts[i]),
                (uint)_runQuadCounts[i]
            );

            for (var n = 0; n < _materialScopes.Count; n++)
            {
                _materialScopes[n].Dispose();
            }
        }

        _gl.Disable(EnableCap.Blend);
        return _runCount;
    }

    private void AppendTarget(GLRectRenderTarget target)
    {
        if (!HasSameMaterials(target.Materials))
        {
            CloseRun();
            _activeMaterials = target.Materials;
            _runMaterials.Add(target.Materials);
            _runQuadOffset = _quadCount;
        }

        AddQuad(target.Rect, target.Color);
    }

    private bool HasSameMaterials(GLMaterial[] materials)
    {
        if (_activeMaterials == null || materials.Length != _activeMaterials.Length)
        {
            return false;
        }

        for (var i = 0; i < materials.Length; i++)
        {
            if (!ReferenceEquals(materials[i], _activeMaterials[i]))
            {
                return false;
            }
        }

        return true;
    }

    private void CloseRun()
    {
        if (_quadCount == _runQuadOffset)
        {
            return;
        }

        if (_runCount == _runQuadOffsets.Length)
        {
            int capacity = Math.Max(8, _runQuadOffsets.Length * 2);
            Array.Resize(ref _runQuadOffsets, capacity);
            Array.Resize(ref _runQuadCounts, capacity);
        }

        _runQuadOffsets[_runCount] = _runQuadOffset;
        _runQuadCounts[_runCount] = _quadCount - _runQuadOffset;
        _runCount++;
    }

    private void AddQuad(Rect2 rect, Vector4 color)
    {
        if (_quadCount == _quadOffsets.Length)
        {
            int capacity = Math.Max(16, _quadOffsets.Length * 2);
            Array.Resize(ref _quadOffsets, capacity);
            Array.Resize(ref _quadCounts, capacity);
        }

        int required = _vertexCount + (VERTICES_PER_QUAD * VERTEX_STRIDE);
        if (_vertices.Length < required)
        {
            Array.Resize(ref _vertices, Math.Max(required, Math.Max(256, _vertices.Length * 2)));
        }

        _quadOffsets[_quadCount] = _vertexCount / VERTEX_STRIDE;
        _quadCounts[_quadCount] = VERTICES_PER_QUAD;
        _quadCount++;

        //  Rect coordinates are normalized, so the clip-space mapping is a plain scale.
        float left = (rect.Min.X * 2f) - 1f;
        float bottom = (rect.Min.Y * 2f) - 1f;
        float right = (rect.Max.X * 2f) - 1f;
        float top = (rect.Max.Y * 2f) - 1f;

        WriteVertex(left, bottom, 0f, 1f, color);
        WriteVertex(right, bottom, 1f, 1f, color);
        WriteVertex(right, top, 1f, 0f, color);
        WriteVertex(left, top, 0f, 0f, color);
    }

    private void WriteVertex(float x, float y, float u, float v, in Vector4 color)
    {
        float[] data = _vertices;
        int index = _vertexCount;

        data[index] = x;
        data[index + 1] = y;
        data[index + 2] = 0f;

        data[index + 3] = color.X;
        data[index + 4] = color.Y;
        data[index + 5] = color.Z;
        data[index + 6] = color.W;

        data[index + 7] = u;
        data[index + 8] = v;
        data[index + 9] = 0f;

        _vertexCount = index + VERTEX_STRIDE;
    }
}
