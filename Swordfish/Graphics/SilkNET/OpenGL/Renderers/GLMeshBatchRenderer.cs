using System.Buffers;
using System.Numerics;
using Silk.NET.OpenGL;
using Swordfish.Library.Extensions;
using Swordfish.Settings;

namespace Swordfish.Graphics.SilkNET.OpenGL.Renderers;

/// <summary>
/// Draws <see cref="MeshBatch"/> ranges with one multi-draw call per arena per material.
/// </summary>
internal sealed class GLMeshBatchRenderer(
    in GL gl,
    in RenderSettings renderSettings,
    in GLRenderContext renderContext
) : IWorldSpaceRenderStage
{
    private readonly GL _gl = gl;
    private readonly RenderSettings _renderSettings = renderSettings;
    private readonly GLRenderContext _renderContext = renderContext;
    private readonly List<RangeCommand> _commands = [];

    private BatchEntityModel[]? _models;
    private ShaderProgram? _depthShader;
    private ShaderProgram? _depthCutoutShader;
    private ShaderProgram? _depthCutoutArrayShader;

    public void SetDepthShaders(ShaderProgram depthShader, ShaderProgram depthCutoutShader, ShaderProgram depthCutoutArrayShader)
    {
        _depthShader = depthShader;
        _depthCutoutShader = depthCutoutShader;
        _depthCutoutArrayShader = depthCutoutArrayShader;
    }

    public void PreRender(double delta, RenderScene renderScene, bool isDepthPass)
    {
        _models = renderScene.BatchEntityModels;
        if (_models == null)
        {
            return;
        }

        for (var i = 0; i < _models.Length; i++)
        {
            BatchEntityModel model = _models[i];
            if (_renderContext.TryGetBatchTarget(model.Renderer, out GLMeshBatch? target) && target != null)
            {
                target.Upload(model.Matrix);
            }
        }
    }

    public int Render(double delta, RenderScene renderScene, Action<ShaderProgram> shaderActivationCallback, bool isDepthPass)
    {
        if (_renderSettings.HideMeshes || renderScene.BatchEntityModels == null || renderScene.BatchEntityModels.Length == 0)
        {
            return 0;
        }

        var drawCalls = 0;
        Vector3 viewPosition = renderScene.View.GetCameraPosition();
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        for (var i = 0; i < renderScene.BatchEntityModels.Length; i++)
        {
            BatchEntityModel model = renderScene.BatchEntityModels[i];
            MeshBatchRenderer renderer = model.Renderer;
            if (!_renderContext.TryGetBatchTarget(renderer, out GLMeshBatch? target) || target == null)
            {
                continue;
            }

            bool transparent = renderer.RenderOptions.AlphaMode == AlphaMode.Blend || HasTransparentMaterial(target.Materials);
            if (isDepthPass && transparent)
            {
                continue;
            }

            if (isDepthPass)
            {
                _gl.Set(EnableCap.DepthTest, !renderer.RenderOptions.IgnoreDepth);
                _gl.Set(EnableCap.CullFace, !renderer.RenderOptions.DoubleFaced);
                _gl.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
                drawCalls += DrawDepth(target, renderer, model.Matrix, renderScene.CameraFrustum, renderScene.View, renderScene.Projection, viewPosition);
            }
            else
            {
                _gl.Set(EnableCap.DepthTest, !renderer.RenderOptions.IgnoreDepth);
                _gl.Set(EnableCap.CullFace, !renderer.RenderOptions.DoubleFaced);
                _gl.PolygonMode(TriangleFace.FrontAndBack, _renderSettings.Wireframe || renderer.RenderOptions.Wireframe ? PolygonMode.Line : PolygonMode.Fill);
                drawCalls += DrawColor(target, renderer, model.Matrix, renderScene.CameraFrustum, renderScene.View, renderScene.Projection, viewPosition, shaderActivationCallback);
            }
        }

        _gl.Disable(EnableCap.Blend);
        return drawCalls;
    }

    public int RenderShadowPass(
        Matrix4x4 view,
        Matrix4x4 projection,
        ShaderProgram depthShader,
        ShaderProgram depthCutoutShader,
        ShaderProgram depthCutoutArrayShader
    ) {
        if (_renderSettings.HideMeshes || _models == null)
        {
            return 0;
        }

        var drawCalls = 0;
        Frustum frustum = Frustum.FromViewProjection(view * projection);

        for (var i = 0; i < _models.Length; i++)
        {
            BatchEntityModel model = _models[i];
            MeshBatchRenderer renderer = model.Renderer;
            if (!renderer.RenderOptions.CastShadows || renderer.RenderOptions.IgnoreDepth)
            {
                continue;
            }

            if (!_renderContext.TryGetBatchTarget(renderer, out GLMeshBatch? target) || target == null)
            {
                continue;
            }

            bool masked = renderer.RenderOptions.AlphaMode == AlphaMode.Masked;
            ShaderProgram? shader = masked ? GetCutoutShader(target) : depthShader;
            if (shader == null)
            {
                continue;
            }

            _gl.Set(EnableCap.CullFace, !renderer.RenderOptions.DoubleFaced);
            _gl.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);

            using var scope = shader.Use();
            shader.SetUniform("view", view);
            shader.SetUniform("projection", projection);
            if (masked)
            {
                shader.SetUniform("texture0", 0);
                shader.SetUniform("uAlphaCutoff", renderer.RenderOptions.AlphaCutoff);
            }

            GLHandle.Scope? textureScope = ActivateTexture(target);
            try
            {
                drawCalls += DrawTarget(target, model.Matrix, frustum, view.GetCameraPosition(), sort: false);
            }
            finally
            {
                textureScope?.Dispose();
            }
        }

        return drawCalls;
    }

    private int DrawColor(
        GLMeshBatch target,
        MeshBatchRenderer renderer,
        in Matrix4x4 matrix,
        in Frustum? frustum,
        in Matrix4x4 view,
        in Matrix4x4 projection,
        in Vector3 viewPosition,
        Action<ShaderProgram> shaderActivationCallback
    ) {
        GLMaterial[] materials = target.Materials;
        GLMaterial.Scope[] scopes = ArrayPool<GLMaterial.Scope>.Shared.Rent(materials.Length);
        try
        {
            for (var i = 0; i < materials.Length; i++)
            {
                GLMaterial material = materials[i];
                ShaderProgram shader = material.ShaderProgram;
                scopes[i] = material.Use();
                shader.SetUniform("view", view);
                shader.SetUniform("projection", projection);
                shader.SetUniform("uCameraPos", viewPosition);
                shader.SetUniform("uReceiveShadows", renderer.RenderOptions.ReceiveShadows ? 1.0f : 0.0f);
                shader.SetUniform("uAlphaCutoff", renderer.RenderOptions.AlphaMode == AlphaMode.Masked ? renderer.RenderOptions.AlphaCutoff : 0.0f);
                shaderActivationCallback(shader);
            }

            return DrawTarget(target, matrix, frustum, viewPosition, sort: renderer.RenderOptions.AlphaMode == AlphaMode.Blend);
        }
        finally
        {
            for (var i = 0; i < materials.Length; i++)
            {
                scopes[i].Dispose();
            }

            ArrayPool<GLMaterial.Scope>.Shared.Return(scopes);
        }
    }

    private int DrawDepth(
        GLMeshBatch target,
        MeshBatchRenderer renderer,
        in Matrix4x4 matrix,
        in Frustum? frustum,
        in Matrix4x4 view,
        in Matrix4x4 projection,
        in Vector3 viewPosition
    ) {
        bool masked = renderer.RenderOptions.AlphaMode == AlphaMode.Masked;
        ShaderProgram? shader = masked ? GetCutoutShader(target) : _depthShader;
        if (shader == null)
        {
            return 0;
        }

        using var scope = shader.Use();
        shader.SetUniform("view", view);
        shader.SetUniform("projection", projection);
        if (masked)
        {
            shader.SetUniform("texture0", 0);
            shader.SetUniform("uAlphaCutoff", renderer.RenderOptions.AlphaCutoff);
        }

        GLHandle.Scope? textureScope = ActivateTexture(target);
        try
        {
            return DrawTarget(target, matrix, frustum, viewPosition, sort: false);
        }
        finally
        {
            textureScope?.Dispose();
        }
    }

    private int DrawTarget(GLMeshBatch target, in Matrix4x4 matrix, in Frustum? frustum, in Vector3 viewPosition, bool sort)
    {
        //  Hold the batch lock across the draw. The ECS thread can update or compact a
        //  range between the command build and the draw.
        lock (target.Batch.SyncRoot)
        {
            _commands.Clear();
            for (var slot = 0; slot < target.Batch.Ranges.Count; slot++)
            {
                MeshRange range = target.Batch.Ranges[slot];
                if (!range.Active || !IsVisible(in range, in matrix, in frustum))
                {
                    continue;
                }

                BoundingBox world = range.Bounds.Transform(matrix);
                _commands.Add(new RangeCommand
                {
                    Arena = range.Arena,
                    IndexOffset = range.IndexOffset * sizeof(uint),
                    IndexCount = range.IndexCount,
                    BaseVertex = range.VertexOffset,
                    DistanceSq = Vector3.DistanceSquared(world.Center, viewPosition),
                });
            }

            if (sort && _commands.Count > 1)
            {
                _commands.Sort(static (a, b) => b.DistanceSq.CompareTo(a.DistanceSq));
            }

            var drawCalls = 0;
            for (var arena = 0; arena < target.Batch.ArenaCount; arena++)
            {
                var count = 0;
                for (var i = 0; i < _commands.Count; i++)
                {
                    if (_commands[i].Arena == arena)
                    {
                        count++;
                    }
                }

                if (count == 0)
                {
                    continue;
                }

                uint[] counts = ArrayPool<uint>.Shared.Rent(count);
                int[] baseVertices = ArrayPool<int>.Shared.Rent(count);
                nint[] offsets = ArrayPool<nint>.Shared.Rent(count);
                try
                {
                    var index = 0;
                    for (var i = 0; i < _commands.Count; i++)
                    {
                        RangeCommand command = _commands[i];
                        if (command.Arena != arena)
                        {
                            continue;
                        }

                        counts[index] = (uint)command.IndexCount;
                        baseVertices[index] = command.BaseVertex;
                        offsets[index] = command.IndexOffset;
                        index++;
                    }

                    target.Draw(arena, counts.AsSpan(0, count), offsets.AsSpan(0, count), baseVertices.AsSpan(0, count));
                    drawCalls++;
                }
                finally
                {
                    ArrayPool<uint>.Shared.Return(counts);
                    ArrayPool<int>.Shared.Return(baseVertices);
                    ArrayPool<nint>.Shared.Return(offsets);
                }
            }

            return drawCalls;
        }
    }

    private ShaderProgram? GetCutoutShader(GLMeshBatch target)
    {
        if (target.Materials.Length == 0 || target.Materials[0].Textures.Length == 0)
        {
            return _depthCutoutShader;
        }

        return target.Materials[0].Textures[0] is TexImage3D ? _depthCutoutArrayShader : _depthCutoutShader;
    }

    private static GLHandle.Scope? ActivateTexture(GLMeshBatch target)
    {
        if (target.Materials.Length > 0 && target.Materials[0].Textures.Length > 0)
        {
            return target.Materials[0].Textures[0].Activate(TextureUnit.Texture0);
        }

        return null;
    }

    private static bool IsVisible(in MeshRange range, in Matrix4x4 matrix, in Frustum? frustum)
    {
        if (!frustum.HasValue)
        {
            return true;
        }

        BoundingBox world = range.Bounds.Transform(matrix);
        return frustum.Value.Intersects(world.Center, world.Extents);
    }

    private static bool HasTransparentMaterial(GLMaterial[] materials)
    {
        for (var i = 0; i < materials.Length; i++)
        {
            if (materials[i].Transparent)
            {
                return true;
            }
        }

        return false;
    }

    private struct RangeCommand
    {
        public int Arena;
        public int IndexOffset;
        public int IndexCount;
        public int BaseVertex;
        public float DistanceSq;
    }
}
