using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.OpenGL;
using Swordfish.ECS;
using Swordfish.Library.Extensions;
using Swordfish.Settings;

namespace Swordfish.Graphics.SilkNET.OpenGL.Renderers;

internal unsafe class GLInstancedRenderer(in GL gl, in RenderSettings renderSettings, in GLRenderContext renderContext) : IWorldSpaceRenderStage
{
    private readonly GL _gl = gl;
    private readonly RenderSettings _renderSettings = renderSettings;
    private readonly GLRenderContext _renderContext = renderContext;

    private readonly Dictionary<GLRenderTarget, InstanceSet> _instances = [];
    private readonly Dictionary<GLRenderTarget, InstanceSet> _maskedInstances = [];
    private readonly Dictionary<GLRenderTarget, InstanceSet> _transparentInstances = [];
    private readonly Stack<InstanceSet> _setPool = new();

    private ShaderProgram? _depthCutoutShader;
    private ShaderProgram? _depthCutoutArrayShader;

    public void SetDepthShaders(ShaderProgram depthShader, ShaderProgram depthCutoutShader, ShaderProgram depthCutoutArrayShader)
    {
        _depthCutoutShader = depthCutoutShader;
        _depthCutoutArrayShader = depthCutoutArrayShader;
    }

    private InstanceSet RentSet()
    {
        if (_setPool.TryPop(out InstanceSet? set))
        {
            set.Clear();
            return set;
        }

        return new InstanceSet();
    }

    private void ReturnSet(InstanceSet set)
    {
        set.Clear();
        _setPool.Push(set);
    }

    public void PreRender(double delta, RenderScene renderScene, bool isDepthPass)
    {
        foreach (InstanceSet set in _instances.Values)
        {
            ReturnSet(set);
        }
        _instances.Clear();

        foreach (InstanceSet set in _maskedInstances.Values)
        {
            ReturnSet(set);
        }
        _maskedInstances.Clear();

        foreach (InstanceSet set in _transparentInstances.Values)
        {
            ReturnSet(set);
        }
        _transparentInstances.Clear();

        for (var i = 0; i < renderScene.EntityModels.Length; i++)
        {
            EntityModel entityModel = renderScene.EntityModels[i];
            if (!_renderContext.TryGetRenderTarget(entityModel.MeshRenderer, out GLRenderTarget? renderTarget) || renderTarget == null)
            {
                continue;
            }

            bool isTransparent = renderTarget.RenderOptions.AlphaMode == AlphaMode.Blend || renderTarget.Materials.Any(material => material.Transparent);
            if (isTransparent && isDepthPass)
            {
                continue;
            }

            Dictionary<GLRenderTarget, InstanceSet> targetDict;
            if (isTransparent)
            {
                targetDict = _transparentInstances;
            }
            else if (renderTarget.RenderOptions.AlphaMode == AlphaMode.Masked)
            {
                targetDict = _maskedInstances;
            }
            else
            {
                targetDict = _instances;
            }

            if (!targetDict.TryGetValue(renderTarget, out InstanceSet? set))
            {
                set = RentSet();
                targetDict.Add(renderTarget, set);
            }

            set.Add(entityModel.Matrix, entityModel.WorldBounds);
        }

        foreach (KeyValuePair<GLRenderTarget, InstanceSet> instance in _instances)
        {
            ReadOnlySpan<Matrix4x4> models = CollectionsMarshal.AsSpan(instance.Value.Matrices);
            UploadModelMatrices(instance.Key, models);
        }

        foreach (KeyValuePair<GLRenderTarget, InstanceSet> instance in _maskedInstances)
        {
            ReadOnlySpan<Matrix4x4> models = CollectionsMarshal.AsSpan(instance.Value.Matrices);
            UploadModelMatrices(instance.Key, models);
        }
    }

    private static bool IsVisible(in InstanceSet set, in Frustum? frustum)
    {
        if (!set.HasBounds || !frustum.HasValue)
        {
            return true;
        }

        return frustum.Value.Intersects(set.Bounds.Center, set.Bounds.Extents);
    }

    private static void UploadModelMatrices(GLRenderTarget target, ReadOnlySpan<Matrix4x4> modelsSpan)
    {
        if (modelsSpan.Length == 0)
        {
            return;
        }

        bool needsUpload = !target.HasUploadedModelMatrix
            || target.LastModelCount != modelsSpan.Length
            || (modelsSpan.Length == 1 ? target.LastModelMatrix != modelsSpan[0] : true);

        if (needsUpload)
        {
            target.ModelsBufferObject.UploadOrResize(modelsSpan);
            target.HasUploadedModelMatrix = true;
            target.LastModelCount = modelsSpan.Length;
            if (modelsSpan.Length == 1)
            {
                target.LastModelMatrix = modelsSpan[0];
            }
        }
    }

    public int Render(double delta, RenderScene renderScene, Action<ShaderProgram> shaderActivationCallback, bool isDepthPass)
    {
        if (renderScene.RenderTargets.Count == 0 || _renderSettings.HideMeshes)
        {
            return 0;
        }

        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        var drawCalls = 0;

        drawCalls += Draw(renderScene.View, renderScene.Projection, shaderActivationCallback, _instances, sort: false, isDepthPass, masked: false, renderScene.CameraFrustum);
        drawCalls += Draw(renderScene.View, renderScene.Projection, shaderActivationCallback, _maskedInstances, sort: false, isDepthPass, masked: true, renderScene.CameraFrustum);

        //  Exclude rendering transparent targets during a depth pass
        if (!isDepthPass)
        {
            drawCalls += Draw(renderScene.View, renderScene.Projection, shaderActivationCallback, _transparentInstances, sort: true, isDepthPass: false, masked: false, renderScene.CameraFrustum);
        }

        _gl.Disable(EnableCap.Blend);
        return drawCalls;
    }

    private int Draw(Matrix4x4 view, Matrix4x4 projection, Action<ShaderProgram> shaderActivationCallback, Dictionary<GLRenderTarget, InstanceSet> instances, bool sort, bool isDepthPass, bool masked, Frustum? frustum)
    {
        if (instances.Count == 0)
        {
            return 0;
        }

        var drawCalls = 0;
        Vector3 viewPosition = view.GetCameraPosition();

        GLMaterial[]? activeMaterials = null;
        GLMaterial.Scope[]? activeScopes = null;
        RenderOptions? lastRenderOptions = null;

        ShaderProgram? activeCutoutShader = null;
        GLHandle.Scope? cutoutScope = null;

        foreach (KeyValuePair<GLRenderTarget, InstanceSet> instance in instances)
        {
            InstanceSet set = instance.Value;
            if (set.Matrices.Count == 0)
            {
                continue;
            }

            if (!IsVisible(set, frustum))
            {
                continue;
            }

            GLRenderTarget target = instance.Key;
            if (target.VertexArrayObject.ElementBufferObject.Length == 0)
            {
                continue;
            }

            List<Matrix4x4> matrices = set.Matrices;
            ReadOnlySpan<Matrix4x4> modelsSpan;
            if (sort)
            {
                if (matrices.Count > 1)
                {
                    matrices.Sort((a, b) => Vector3.DistanceSquared(b.GetPosition(), viewPosition).CompareTo(Vector3.DistanceSquared(a.GetPosition(), viewPosition)));
                }
                modelsSpan = CollectionsMarshal.AsSpan(matrices);
                target.ModelsBufferObject.UploadOrResize(modelsSpan);
            }
            else
            {
                modelsSpan = CollectionsMarshal.AsSpan(matrices);
                UploadModelMatrices(target, modelsSpan);
            }

            if (isDepthPass && masked)
            {
                bool isArray = target.Materials.Length > 0 && target.Materials[0].Textures.Length > 0 && target.Materials[0].Textures[0] is TexImage3D;
                ShaderProgram? cutoutShader = isArray ? _depthCutoutArrayShader : _depthCutoutShader;
                if (cutoutShader != null)
                {
                    if (activeCutoutShader != cutoutShader)
                    {
                        cutoutScope?.Dispose();
                        activeCutoutShader = cutoutShader;
                        cutoutScope = cutoutShader.Use();
                        cutoutShader.SetUniform("view", view);
                        cutoutShader.SetUniform("projection", projection);
                        cutoutShader.SetUniform("texture0", 0);
                    }

                    cutoutShader.SetUniform("uAlphaCutoff", target.RenderOptions.AlphaCutoff);

                    GLHandle.Scope? textureScope = null;
                    if (target.Materials.Length > 0 && target.Materials[0].Textures.Length > 0)
                    {
                        textureScope = target.Materials[0].Textures[0].Activate(TextureUnit.Texture0);
                    }

                    target.VertexArrayObject.Bind();
                    _gl.Set(EnableCap.CullFace, !target.RenderOptions.DoubleFaced);
                    _gl.DrawElementsInstanced(PrimitiveType.Triangles, (uint)target.VertexArrayObject.ElementBufferObject.Length, DrawElementsType.UnsignedInt, (void*)0, (uint)modelsSpan.Length);
                    drawCalls++;
                    textureScope?.Dispose();
                    continue;
                }
            }

            if (!isDepthPass)
            {
                bool materialsMatch = activeMaterials != null && activeMaterials.Length == target.Materials.Length;
                if (materialsMatch)
                {
                    for (var n = 0; n < target.Materials.Length; n++)
                    {
                        if (activeMaterials![n] != target.Materials[n])
                        {
                            materialsMatch = false;
                            break;
                        }
                    }
                }

                if (!materialsMatch)
                {
                    if (activeScopes != null && activeMaterials != null)
                    {
                        for (var n = 0; n < activeMaterials.Length; n++)
                        {
                            activeScopes[n].Dispose();
                        }
                        ArrayPool<GLMaterial.Scope>.Shared.Return(activeScopes);
                    }

                    activeMaterials = target.Materials;
                    activeScopes = ArrayPool<GLMaterial.Scope>.Shared.Rent(target.Materials.Length);
                    for (var n = 0; n < target.Materials.Length; n++)
                    {
                        GLMaterial material = target.Materials[n];
                        ShaderProgram shader = material.ShaderProgram;

                        activeScopes[n] = material.Use();
                        shader.SetUniform("view", view);
                        shader.SetUniform("projection", projection);
                        shader.SetUniform("uCameraPos", viewPosition);
                        shaderActivationCallback(shader);
                    }
                }

                if (!materialsMatch || lastRenderOptions == null || !lastRenderOptions.Value.Equals(target.RenderOptions))
                {
                    for (var n = 0; n < activeMaterials!.Length; n++)
                    {
                        ShaderProgram shader = activeMaterials[n].ShaderProgram;
                        shader.SetUniform("uReceiveShadows", target.RenderOptions.ReceiveShadows ? 1.0f : 0.0f);
                        shader.SetUniform("uAlphaCutoff", target.RenderOptions.AlphaMode == AlphaMode.Masked ? target.RenderOptions.AlphaCutoff : 0.0f);
                    }
                }
            }

            target.VertexArrayObject.Bind();

            if (lastRenderOptions == null || !lastRenderOptions.Value.Equals(target.RenderOptions))
            {
                _gl.Set(EnableCap.DepthTest, !target.RenderOptions.IgnoreDepth);
                _gl.Set(EnableCap.CullFace, !target.RenderOptions.DoubleFaced);
                _gl.PolygonMode(TriangleFace.FrontAndBack, _renderSettings.Wireframe || target.RenderOptions.Wireframe ? PolygonMode.Line : PolygonMode.Fill);
                lastRenderOptions = target.RenderOptions;
            }

            _gl.DrawElementsInstanced(PrimitiveType.Triangles, (uint)target.VertexArrayObject.ElementBufferObject.Length, DrawElementsType.UnsignedInt, (void*)0, (uint)modelsSpan.Length);
            drawCalls++;
        }

        if (activeScopes != null && activeMaterials != null)
        {
            for (var n = 0; n < activeMaterials.Length; n++)
            {
                activeScopes[n].Dispose();
            }
            ArrayPool<GLMaterial.Scope>.Shared.Return(activeScopes);
        }

        cutoutScope?.Dispose();

        return drawCalls;
    }

    public int RenderShadowPass(
        Matrix4x4 view,
        Matrix4x4 projection,
        ShaderProgram depthShader,
        ShaderProgram depthCutoutShader,
        ShaderProgram depthCutoutArrayShader
    ) {
        if ((_instances.Count == 0 && _maskedInstances.Count == 0) || _renderSettings.HideMeshes)
        {
            return 0;
        }

        Frustum cullFrustum = Frustum.FromViewProjection(view * projection);
        var drawCalls = 0;

        if (_instances.Count > 0)
        {
            using var _ = depthShader.Use();
            depthShader.SetUniform("view", view);
            depthShader.SetUniform("projection", projection);

            foreach (KeyValuePair<GLRenderTarget, InstanceSet> instance in _instances)
            {
                GLRenderTarget target = instance.Key;
                if (!target.RenderOptions.CastShadows || target.RenderOptions.IgnoreDepth)
                {
                    continue;
                }

                InstanceSet set = instance.Value;
                if (set.Matrices.Count == 0
                    || target.VertexArrayObject.ElementBufferObject.Length == 0
                    || !IsVisible(set, cullFrustum)
                ) {
                    continue;
                }

                target.VertexArrayObject.Bind();

                _gl.Set(EnableCap.DepthTest, true);
                _gl.Set(EnableCap.CullFace, !target.RenderOptions.DoubleFaced);
                _gl.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
                _gl.DrawElementsInstanced(PrimitiveType.Triangles, (uint)target.VertexArrayObject.ElementBufferObject.Length, DrawElementsType.UnsignedInt, (void*)0, (uint)set.Matrices.Count);
                drawCalls++;
            }
        }

        if (_maskedInstances.Count > 0)
        {
            _gl.Set(EnableCap.DepthTest, true);

            ShaderProgram? activeCutoutShader = null;
            GLHandle.Scope? cutoutScope = null;

            foreach (KeyValuePair<GLRenderTarget, InstanceSet> instance in _maskedInstances)
            {
                GLRenderTarget target = instance.Key;
                if (!target.RenderOptions.CastShadows || target.RenderOptions.IgnoreDepth)
                {
                    continue;
                }

                InstanceSet set = instance.Value;
                if (set.Matrices.Count == 0
                    || target.VertexArrayObject.ElementBufferObject.Length == 0
                    || !IsVisible(set, cullFrustum)
                ) {
                    continue;
                }

                bool isArray = target.Materials.Length > 0 && target.Materials[0].Textures.Length > 0 && target.Materials[0].Textures[0] is TexImage3D;
                ShaderProgram cutoutShader = isArray ? depthCutoutArrayShader : depthCutoutShader;

                if (activeCutoutShader != cutoutShader)
                {
                    cutoutScope?.Dispose();
                    activeCutoutShader = cutoutShader;
                    cutoutScope = cutoutShader.Use();
                    cutoutShader.SetUniform("view", view);
                    cutoutShader.SetUniform("projection", projection);
                    cutoutShader.SetUniform("texture0", 0);
                }

                cutoutShader.SetUniform("uAlphaCutoff", target.RenderOptions.AlphaCutoff);

                GLHandle.Scope? texScope = null;
                if (target.Materials.Length > 0 && target.Materials[0].Textures.Length > 0)
                {
                    texScope = target.Materials[0].Textures[0].Activate(TextureUnit.Texture0);
                }

                target.VertexArrayObject.Bind();

                _gl.Set(EnableCap.CullFace, !target.RenderOptions.DoubleFaced);
                _gl.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
                _gl.DrawElementsInstanced(PrimitiveType.Triangles, (uint)target.VertexArrayObject.ElementBufferObject.Length, DrawElementsType.UnsignedInt, (void*)0, (uint)set.Matrices.Count);
                drawCalls++;

                texScope?.Dispose();
            }

            cutoutScope?.Dispose();
        }

        return drawCalls;
    }

    private sealed class InstanceSet
    {
        public readonly List<Matrix4x4> Matrices = new(4);
        public BoundingBox Bounds;
        public bool HasBounds;

        public void Clear()
        {
            Matrices.Clear();
            HasBounds = false;
        }

        public void Add(in Matrix4x4 matrix, in BoundingBox worldBounds)
        {
            Matrices.Add(matrix);

            if (!HasBounds)
            {
                Bounds = worldBounds;
                HasBounds = true;
                return;
            }

            Bounds = BoundingBox.Union(Bounds, worldBounds);
        }
    }
}
