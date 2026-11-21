using System.Numerics;
using JoltPhysicsSharp;
using Microsoft.Extensions.Logging;
using Silk.NET.OpenGL;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Graphics.SilkNET.OpenGL.Util;
using Swordfish.Library.Collections;
using Swordfish.Library.Extensions;
using Swordfish.Library.Util;
using Swordfish.Physics.Jolt;
using Swordfish.Settings;

namespace Swordfish.Graphics.Jolt;

/// <summary>
///     Renders Jolt physics debug geometry directly with streaming OpenGL vertex batches.
/// </summary>
internal sealed unsafe class JoltDebugRenderer : DebugRenderer, IWorldSpaceRenderStage
{
    private readonly GL _gl;
    private readonly DebugSettings _debugSettings;
    private readonly IJoltPhysics _joltPhysics;
    private readonly JoltDebugBatch _batch = new();
    private readonly FrustumBodyDrawFilter _bodyFilter = new();
    private readonly ShaderProgram? _shaderProgram;
    private readonly VertexArrayObject<JoltDebugVertex>? _lineVao;
    private readonly VertexArrayObject<JoltDebugVertex>? _triangleVao;
    private readonly DrawSettings _drawSettings = new()
    {
        DrawShapeWireframe = true,
        DrawVelocity = true,
    };

    /// <summary>Creates a Jolt debug renderer backed by streaming OpenGL line and triangle batches.</summary>
    public JoltDebugRenderer(
        in ILogger logger,
        in GL gl,
        in GLContext glContext,
        in IAssetDatabase<Shader> shaderDatabase,
        in DebugSettings debugSettings,
        in IJoltPhysics joltPhysics
    ) {
        _gl = gl;
        _debugSettings = debugSettings;
        _joltPhysics = joltPhysics;

        Result<Shader> shaderResult = shaderDatabase.Get("lines");
        if (!shaderResult)
        {
            logger.LogError("The shader \"lines\" was not found. Jolt physics debug geometry will not be rendered.");
            return;
        }

        _shaderProgram = shaderResult.Value.CreateProgram(glContext);
        _lineVao = CreateVao(glContext);
        _triangleVao = CreateVao(glContext);
    }

    /// <inheritdoc/>
    public void PreRender(double delta, RenderScene renderScene, bool isDepthPass)
    {
    }

    /// <inheritdoc/>
    public int Render(double delta, RenderScene renderScene, Action<ShaderProgram> shaderActivationCallback, bool isDepthPass)
    {
        if (isDepthPass || !_debugSettings.Gizmos.Physics || _shaderProgram == null || _lineVao == null || _triangleVao == null)
        {
            return 0;
        }

        _batch.Clear();
        SetCameraPosition(renderScene.View.GetCameraPosition());
        _bodyFilter.SetFrustum(renderScene.CameraFrustum);
        _joltPhysics.DrawBodies(_drawSettings, this, _bodyFilter);
        NextFrame();

        if (_batch.IsEmpty)
        {
            return 0;
        }

        var drawCalls = 0;

        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _gl.Enable(EnableCap.DepthTest);
        _gl.DepthMask(false);

        using (GLHandle.Scope _ = _shaderProgram.Use())
        {
            _shaderProgram.SetUniform("view", renderScene.View);
            _shaderProgram.SetUniform("projection", renderScene.Projection);
            shaderActivationCallback(_shaderProgram);

            if (_batch.TriangleVertexCount > 0)
            {
                _gl.Disable(EnableCap.CullFace);
                _gl.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
                _triangleVao.VertexBufferObject.UpdateStreamingData(_batch.TriangleVertices);
                _triangleVao.Bind();
                _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)_batch.TriangleVertexCount);
                _triangleVao.Unbind();
                _gl.Enable(EnableCap.CullFace);
                drawCalls++;
            }

            if (_batch.LineVertexCount > 0)
            {
                _lineVao.VertexBufferObject.UpdateStreamingData(_batch.LineVertices);
                _lineVao.Bind();
                _gl.DrawArrays(PrimitiveType.Lines, 0, (uint)_batch.LineVertexCount);
                _lineVao.Unbind();
                drawCalls++;
            }
        }

        _gl.DepthMask(true);
        _gl.Disable(EnableCap.Blend);
        return drawCalls;
    }

    /// <inheritdoc/>
    protected override void DrawLine(Vector3 from, Vector3 to, JoltColor color)
    {
        _batch.AddLine(from, to, color.PackedValue);
    }

    /// <inheritdoc/>
    protected override void DrawTriangle(Vector3 v1, Vector3 v2, Vector3 v3, JoltColor color, CastShadow castShadow)
    {
        _batch.AddTriangle(v1, v2, v3, color.PackedValue);
    }

    /// <inheritdoc/>
    protected override void DrawText3D(Vector3 position, string? text, JoltColor color, float height = 0.5f)
    {
    }

    /// <inheritdoc/>
    protected override void DisposeManaged()
    {
        _lineVao?.Dispose();
        _triangleVao?.Dispose();
        _shaderProgram?.Dispose();
        _bodyFilter.Dispose();
        base.DisposeManaged();
    }

    private static VertexArrayObject<JoltDebugVertex> CreateVao(GLContext glContext)
    {
        VertexArrayObject<JoltDebugVertex> vao = glContext.CreateVertexArrayObject(Array.Empty<JoltDebugVertex>());
        uint stride = (uint)sizeof(JoltDebugVertex);
        vao.VertexBufferObject.Bind();
        vao.SetVertexAttributePointer(index: 0, count: 3, type: VertexAttribPointerType.Float, stride: stride, offset: 0);
        vao.SetVertexAttributePointer(index: 1, count: 4, type: VertexAttribPointerType.UnsignedByte, stride: stride, offset: sizeof(Vector3), normalized: true);
        vao.VertexBufferObject.Unbind();
        return vao;
    }
}
