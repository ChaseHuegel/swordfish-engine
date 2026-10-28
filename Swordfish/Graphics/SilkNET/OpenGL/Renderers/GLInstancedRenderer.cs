using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.OpenGL;
using Swordfish.ECS;
using Swordfish.Library.Extensions;
using Swordfish.Settings;

namespace Swordfish.Graphics.SilkNET.OpenGL.Renderers;

internal unsafe class GLInstancedRenderer(in GL gl, in RenderSettings renderSettings) : IWorldSpaceRenderStage
{
    private readonly GL _gl = gl;
    private readonly RenderSettings _renderSettings = renderSettings;

    private readonly Dictionary<GLRenderTarget, List<Matrix4x4>> _instances = [];
    private readonly Dictionary<GLRenderTarget, List<Matrix4x4>> _maskedInstances = [];
    private readonly Dictionary<GLRenderTarget, List<Matrix4x4>> _transparentInstances = [];
    private readonly Dictionary<Uuid, List<Matrix4x4>> _entityTargetLists = [];
    
    public void PreRender(double delta, RenderScene renderScene, bool isDepthPass)
    {
        _instances.Clear();
        _maskedInstances.Clear();
        _transparentInstances.Clear();
        _entityTargetLists.Clear();
        
        renderScene.RenderTargets.ForEach(ForEachRenderTarget);
        void ForEachRenderTarget(GLRenderTarget renderTarget)
        {
            List<Matrix4x4>? matrices;
            if (renderTarget.RenderOptions.AlphaMode == AlphaMode.Blend || renderTarget.Materials.Any(material => material.Transparent))
            {
                //  Exclude anything with blend transparency from depth passes
                if (isDepthPass)
                {
                    return;
                }
                
                if (!_transparentInstances.TryGetValue(renderTarget, out matrices))
                {
                    matrices = [];
                    _transparentInstances.TryAdd(renderTarget, matrices);
                }
            }
            else if (renderTarget.RenderOptions.AlphaMode == AlphaMode.Masked)
            {
                if (!_maskedInstances.TryGetValue(renderTarget, out matrices))
                {
                    matrices = [];
                    _maskedInstances.TryAdd(renderTarget, matrices);
                }
            }
            else
            {
                if (!_instances.TryGetValue(renderTarget, out matrices))
                {
                    matrices = [];
                    _instances.TryAdd(renderTarget, matrices);
                }
            }

            _entityTargetLists[renderTarget.Entity] = matrices;
        }

        for (var i = 0; i < renderScene.EntityModels.Length; i++)
        {
            EntityModel entityModel = renderScene.EntityModels[i];
            if (_entityTargetLists.TryGetValue(entityModel.Entity, out List<Matrix4x4>? matrices))
            {
                matrices.Add(entityModel.Matrix);
            }
        }

        foreach (KeyValuePair<GLRenderTarget, List<Matrix4x4>> instance in _instances)
        {
            ReadOnlySpan<Matrix4x4> models = CollectionsMarshal.AsSpan(instance.Value);
            if (models.Length > 0)
            {
                instance.Key.ModelsBufferObject.UpdateData(models);
            }
        }

        foreach (KeyValuePair<GLRenderTarget, List<Matrix4x4>> instance in _maskedInstances)
        {
            ReadOnlySpan<Matrix4x4> models = CollectionsMarshal.AsSpan(instance.Value);
            if (models.Length > 0)
            {
                instance.Key.ModelsBufferObject.UpdateData(models);
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

        drawCalls += Draw(renderScene.View, renderScene.Projection, shaderActivationCallback, _instances, sort: false);
        drawCalls += Draw(renderScene.View, renderScene.Projection, shaderActivationCallback, _maskedInstances, sort: false);

        //  Exclude rendering transparent targets during a depth pass
        if (!isDepthPass)
        {
            drawCalls += Draw(renderScene.View, renderScene.Projection, shaderActivationCallback, _transparentInstances, sort: true);
        }

        _gl.Disable(EnableCap.Blend);
        return drawCalls;
    }

    private int Draw(Matrix4x4 view, Matrix4x4 projection, Action<ShaderProgram> shaderActivationCallback, Dictionary<GLRenderTarget, List<Matrix4x4>> instances, bool sort)
    {
        if (instances.Count == 0)
        {
            return 0;
        }

        var drawCalls = 0;
        Vector3 viewPosition = view.GetCameraPosition();

        foreach (KeyValuePair<GLRenderTarget, List<Matrix4x4>> instance in instances)
        {
            GLRenderTarget target = instance.Key;
            List<Matrix4x4> list = instance.Value;
            if (list.Count == 0 || target.VertexArrayObject.ElementBufferObject.Length == 0)
            {
                continue;
            }

            if (sort)
            {
                list.Sort((a, b) => Vector3.DistanceSquared(b.GetPosition(), viewPosition).CompareTo(Vector3.DistanceSquared(a.GetPosition(), viewPosition)));
                target.ModelsBufferObject.UpdateData(CollectionsMarshal.AsSpan(list));
            }

            GLMaterial.Scope[] materialScopes = ArrayPool<GLMaterial.Scope>.Shared.Rent(target.Materials.Length);
            for (var n = 0; n < target.Materials.Length; n++)
            {
                GLMaterial material = target.Materials[n];
                ShaderProgram shader = material.ShaderProgram;
                
                materialScopes[n] = material.Use();
                shader.SetUniform("view", view);
                shader.SetUniform("projection", projection);
                shader.SetUniform("uCameraPos", viewPosition);
                shader.SetUniform("uReceiveShadows", target.RenderOptions.ReceiveShadows ? 1.0f : 0.0f);
                shader.SetUniform("uAlphaCutoff", target.RenderOptions.AlphaMode == AlphaMode.Masked ? target.RenderOptions.AlphaCutoff : 0.0f);
                shaderActivationCallback(shader);
            }

            target.VertexArrayObject.Bind();

            _gl.Set(EnableCap.DepthTest, !target.RenderOptions.IgnoreDepth);
            _gl.Set(EnableCap.CullFace, !target.RenderOptions.DoubleFaced);
            _gl.PolygonMode(TriangleFace.FrontAndBack, _renderSettings.Wireframe || target.RenderOptions.Wireframe ? PolygonMode.Line : PolygonMode.Fill);
            _gl.DrawElementsInstanced(PrimitiveType.Triangles, (uint)target.VertexArrayObject.ElementBufferObject.Length, DrawElementsType.UnsignedInt, (void*)0, (uint)list.Count);
            drawCalls++;

            for (var n = 0; n < target.Materials.Length; n++)
            {
                materialScopes[n].Dispose();
            }
            ArrayPool<GLMaterial.Scope>.Shared.Return(materialScopes);
        }

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

        var drawCalls = 0;

        if (_instances.Count > 0)
        {
            using var _ = depthShader.Use();
            depthShader.SetUniform("view", view);
            depthShader.SetUniform("projection", projection);

            foreach (KeyValuePair<GLRenderTarget, List<Matrix4x4>> instance in _instances)
            {
                GLRenderTarget target = instance.Key;
                if (!target.RenderOptions.CastShadows || target.RenderOptions.IgnoreDepth)
                {
                    continue;
                }

                int count = instance.Value.Count;
                if (count == 0 || target.VertexArrayObject.ElementBufferObject.Length == 0)
                {
                    continue;
                }

                target.VertexArrayObject.Bind();

                _gl.Set(EnableCap.DepthTest, true);
                _gl.Set(EnableCap.CullFace, !target.RenderOptions.DoubleFaced);
                _gl.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
                _gl.DrawElementsInstanced(PrimitiveType.Triangles, (uint)target.VertexArrayObject.ElementBufferObject.Length, DrawElementsType.UnsignedInt, (void*)0, (uint)count);
                drawCalls++;
            }
        }

        if (_maskedInstances.Count > 0)
        {
            _gl.Set(EnableCap.DepthTest, true);

            ShaderProgram? activeCutoutShader = null;
            GLHandle.Scope? cutoutScope = null;

            foreach (KeyValuePair<GLRenderTarget, List<Matrix4x4>> instance in _maskedInstances)
            {
                GLRenderTarget target = instance.Key;
                if (!target.RenderOptions.CastShadows || target.RenderOptions.IgnoreDepth)
                {
                    continue;
                }

                int count = instance.Value.Count;
                if (count == 0 || target.VertexArrayObject.ElementBufferObject.Length == 0)
                {
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
                _gl.DrawElementsInstanced(PrimitiveType.Triangles, (uint)target.VertexArrayObject.ElementBufferObject.Length, DrawElementsType.UnsignedInt, (void*)0, (uint)count);
                drawCalls++;

                texScope?.Dispose();
            }

            cutoutScope?.Dispose();
        }

        return drawCalls;
    }
}