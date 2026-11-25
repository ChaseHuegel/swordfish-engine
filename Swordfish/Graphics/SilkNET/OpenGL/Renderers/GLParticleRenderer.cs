using System.Numerics;
using Microsoft.Extensions.Logging;
using Silk.NET.OpenGL;
using Swordfish.Graphics.Particles;
using Swordfish.Graphics.SilkNET.OpenGL.Util;
using Swordfish.IO;
using Swordfish.Library.Extensions;
using Swordfish.Library.IO;
using Swordfish.Settings;

namespace Swordfish.Graphics.SilkNET.OpenGL.Renderers;

/// <summary>Draws active particle batches as instanced camera-facing billboards in the transparent pass.</summary>
internal sealed unsafe class GLParticleRenderer : IWorldSpaceRenderStage, IDisposable
{
    private const int QUAD_VERTEX_STRIDE = 6;
    private const uint QUAD_INDEX_COUNT = 6;

    private static readonly float[] _quadVertices =
    [
        -0.5f, -0.5f, 0f, 0f, 0f, 0f,
         0.5f, -0.5f, 0f, 1f, 0f, 0f,
         0.5f,  0.5f, 0f, 1f, 1f, 0f,
        -0.5f,  0.5f, 0f, 0f, 1f, 0f,
    ];

    private static readonly uint[] _quadIndices = [0, 1, 2, 0, 2, 3];

    private readonly GL _gl;
    private readonly GLRenderContext _renderContext;
    private readonly RenderSettings _renderSettings;
    private readonly ParticleSystem _particleSystem;
    private readonly ShaderProgram? _shaderProgram;
    private readonly VertexArrayObject32? _vao;
    private readonly BufferObject<ParticleInstance>? _instanceVbo;

    private ParticleRenderSnapshot? _snapshot;
    private float[] _sortKeys = [];
    private int[] _sortOrder = [];
    private ParticleInstance[] _sortScratch = [];

    /// <summary>Creates the particle render stage and allocates its shared quad and instance buffers.</summary>
    public GLParticleRenderer(
        in ILogger<GLParticleRenderer> logger,
        in GL gl,
        in GLContext glContext,
        in GLRenderContext renderContext,
        in RenderSettings renderSettings,
        in ParticleSystem particleSystem,
        in IFileParseService fileParseService,
        in VirtualFileSystem vfs
    ) {
        _gl = gl;
        _renderContext = renderContext;
        _renderSettings = renderSettings;
        _particleSystem = particleSystem;

        if (!vfs.TryGetFile(AssetPaths.Shaders.At("particles.glsl"), out PathInfo shaderFile))
        {
            logger.LogError("The shader source for OpenGL particles was not found. Particles will not be rendered.");
            return;
        }

        if (!fileParseService.TryParse(shaderFile, out Shader shader))
        {
            logger.LogError("Failed to parse the OpenGL particles shader. Particles will not be rendered.");
            return;
        }

        _shaderProgram = shader.CreateProgram(glContext);
        _vao = glContext.CreateVertexArrayObject32(_quadVertices, _quadIndices);
        _instanceVbo = glContext.CreateBufferObject(Array.Empty<ParticleInstance>(), BufferTargetARB.ArrayBuffer, BufferUsageARB.StreamDraw);

        _vao.Bind();
        _vao.VertexBufferObject.Bind();
        _vao.SetVertexAttribute(0, 3, VertexAttribPointerType.Float, QUAD_VERTEX_STRIDE, 0);
        _vao.SetVertexAttribute(2, 3, VertexAttribPointerType.Float, QUAD_VERTEX_STRIDE, 3);

        _instanceVbo.Bind();
        uint instanceStride = (uint)sizeof(ParticleInstance);
        _vao.SetVertexAttributePointer(8, 4, VertexAttribPointerType.Float, instanceStride, 0);
        _vao.SetVertexAttributeDivisor(8, 1);
        _vao.SetVertexAttributePointer(9, 4, VertexAttribPointerType.Float, instanceStride, 16);
        _vao.SetVertexAttributeDivisor(9, 1);
        _vao.SetVertexAttributePointer(10, 2, VertexAttribPointerType.Float, instanceStride, 32);
        _vao.SetVertexAttributeDivisor(10, 1);

        _instanceVbo.Unbind();
        _vao.Unbind();
    }

    /// <inheritdoc/>
    public void PreRender(double delta, RenderScene renderScene, bool isDepthPass)
    {
        _snapshot = _particleSystem.AcquireRenderSnapshot();
    }

    /// <inheritdoc/>
    public int Render(double delta, RenderScene renderScene, Action<ShaderProgram> shaderActivationCallback, bool isDepthPass)
    {
        return 0;
    }

    /// <inheritdoc/>
    public void CollectTransparent(RenderScene renderScene, int stageIndex, List<TransparentDrawItem> items)
    {
        if (_shaderProgram == null || _vao == null || _instanceVbo == null || _renderSettings.HideMeshes || !_renderSettings.Particles.Enabled.Get())
        {
            return;
        }

        ParticleRenderSnapshot? snapshot = _snapshot;
        if (snapshot == null || snapshot.BatchCount == 0 || snapshot.InstanceCount == 0)
        {
            return;
        }

        Vector3 viewPosition = renderScene.View.GetCameraPosition();
        Frustum? frustum = renderScene.CameraFrustum;

        for (var i = 0; i < snapshot.BatchCount; i++)
        {
            ParticleRenderBatch batch = snapshot.Batches[i];
            if (batch.Count <= 0)
            {
                continue;
            }

            if (frustum.HasValue && !frustum.Value.Intersects(batch.Bounds.Center, batch.Bounds.Extents))
            {
                continue;
            }

            if (batch.BlendMode == ParticleBlendMode.Alpha && batch.Count > 1)
            {
                SortBatchBackToFront(snapshot.Instances, batch.Offset, batch.Count, in viewPosition);
            }

            items.Add(new TransparentDrawItem(stageIndex, i, batch.Bounds.DistanceSquaredTo(viewPosition)));
        }
    }

    /// <inheritdoc/>
    public int RenderTransparentItem(int itemIndex, RenderScene renderScene, Action<ShaderProgram> shaderActivationCallback)
    {
        ParticleRenderSnapshot? snapshot = _snapshot;
        if (_shaderProgram == null || _vao == null || _instanceVbo == null || snapshot == null || itemIndex < 0 || itemIndex >= snapshot.BatchCount)
        {
            return 0;
        }

        ParticleRenderBatch batch = snapshot.Batches[itemIndex];
        if (batch.Count <= 0)
        {
            return 0;
        }

        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(
            BlendingFactor.SrcAlpha,
            batch.BlendMode == ParticleBlendMode.Additive ? BlendingFactor.One : BlendingFactor.OneMinusSrcAlpha
        );
        _gl.Enable(EnableCap.DepthTest);
        _gl.DepthMask(false);
        _gl.Disable(EnableCap.CullFace);
        _gl.PolygonMode(TriangleFace.FrontAndBack, _renderSettings.Wireframe ? PolygonMode.Line : PolygonMode.Fill);

        GLHandle.Scope? textureScope = null;
        try
        {
            using GLHandle.Scope shaderScope = _shaderProgram.Use();
            _shaderProgram.SetUniform("view", renderScene.View);
            _shaderProgram.SetUniform("projection", renderScene.Projection);
            shaderActivationCallback(_shaderProgram);

            if (batch.Texture != null)
            {
                IGLTexture? glTexture = _renderContext.TryBindTexture(batch.Texture);
                if (glTexture == null)
                {
                    return 0;
                }

                textureScope = glTexture.Activate(TextureUnit.Texture0);
                _shaderProgram.SetUniform("texture0", 0);
                _shaderProgram.SetUniform("uHasTexture", 1);
            }
            else
            {
                _shaderProgram.SetUniform("uHasTexture", 0);
            }

            _instanceVbo.UpdateStreamingData(snapshot.Instances.AsSpan(batch.Offset, batch.Count));

            _vao.Bind();
            _gl.DrawElementsInstanced(PrimitiveType.Triangles, QUAD_INDEX_COUNT, DrawElementsType.UnsignedInt, (void*)0, (uint)batch.Count);
            _vao.Unbind();
            return 1;
        }
        finally
        {
            textureScope?.Dispose();
            _gl.CullFace(TriangleFace.Back);
            _gl.Enable(EnableCap.CullFace);
            _gl.DepthMask(true);
            _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            _gl.Disable(EnableCap.Blend);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _vao?.Dispose();
        _instanceVbo?.Dispose();
        _shaderProgram?.Dispose();
    }

    private void SortBatchBackToFront(ParticleInstance[] instances, int offset, int count, in Vector3 viewPosition)
    {
        EnsureSortCapacity(count);

        for (var i = 0; i < count; i++)
        {
            _sortKeys[i] = -Vector3.DistanceSquared(instances[offset + i].Position, viewPosition);
            _sortOrder[i] = i;
            _sortScratch[i] = instances[offset + i];
        }

        Array.Sort(_sortKeys, _sortOrder, 0, count);

        for (var i = 0; i < count; i++)
        {
            instances[offset + i] = _sortScratch[_sortOrder[i]];
        }
    }

    private void EnsureSortCapacity(int required)
    {
        if (_sortKeys.Length >= required)
        {
            return;
        }

        int next = Math.Max(required, Math.Max(64, _sortKeys.Length * 2));
        _sortKeys = new float[next];
        _sortOrder = new int[next];
        _sortScratch = new ParticleInstance[next];
    }
}
