using System.Buffers;
using System.Numerics;
using Silk.NET.OpenGL;
using Swordfish.Library.Collections;
using Swordfish.Library.Extensions;
using Swordfish.Settings;

namespace Swordfish.Graphics.SilkNET.OpenGL.Renderers;

internal unsafe class GLInstancedRenderer(in GL gl, in RenderSettings renderSettings) : IWorldSpaceRenderStage
{
    private readonly GL _gl = gl;
    private readonly RenderSettings _renderSettings = renderSettings;
    
    private readonly Dictionary<GLRenderTarget, List<Matrix4x4>> _instances = [];
    private readonly Dictionary<GLRenderTarget, List<Matrix4x4>> _transparentInstances = [];
    private LockedList<GLRenderTarget>? _renderTargets;

    public void Initialize(IRenderContext renderContext)
    {
        if (renderContext is not GLRenderContext glRenderContext)
        {
            throw new NotSupportedException($"{nameof(GLInstancedRenderer)} only supports an OpenGL {nameof(IRenderContext)}.");
        }

        //  TODO this is bad
        _renderTargets = glRenderContext.RenderTargets;
    }

    public void PreRender(double delta, Matrix4x4 view, Matrix4x4 projection, bool isDepthPass)
    {
        if (_renderTargets == null)
        {
            throw new InvalidOperationException($"{nameof(PreRender)} was called without initializing a valid render targets collection.");
        }

        _instances.Clear();
        _transparentInstances.Clear();
        _renderTargets.ForEach(ForEachRenderTarget);
        return;

        void ForEachRenderTarget(GLRenderTarget renderTarget)
        {
            List<Matrix4x4>? matrices;

            if (renderTarget.Materials.Any(material => material.Transparent))
            {
                //  Exclude anything with transparency from depth passes
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
            else
            {
                if (!_instances.TryGetValue(renderTarget, out matrices))
                {
                    matrices = [];
                    _instances.TryAdd(renderTarget, matrices);
                }
            }

            matrices.Add(renderTarget.Transform.ToMatrix4X4());
        }
    }

    public int Render(double delta, Matrix4x4 view, Matrix4x4 projection, Action<ShaderProgram> shaderActivationCallback, bool isDepthPass)
    {
        if (_renderTargets == null)
        {
            throw new InvalidOperationException($"{nameof(Render)} was called without initializing a valid render targets collection.");
        }

        if (_renderTargets.Count == 0 || _renderSettings.HideMeshes)
        {
            return 0;
        }
        
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        var drawCalls = 0;
        
        drawCalls += Draw(view, projection, shaderActivationCallback, _instances, sort: false);
        
        //  Exclude rendering transparent targets during a depth pass
        if (!isDepthPass)
        {
            drawCalls += Draw(view, projection, shaderActivationCallback, _transparentInstances, sort: true);
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
        Vector3 viewPosition = view.GetPosition();

        foreach (KeyValuePair<GLRenderTarget, List<Matrix4x4>> instance in instances)
        {
            GLRenderTarget target = instance.Key;
            IEnumerable<Matrix4x4> matrices = sort ? instance.Value.OrderBy(model => Vector3.DistanceSquared(model.GetPosition(), viewPosition)) : instance.Value;
            Matrix4x4[] models = [.. matrices];

            target.ModelsBufferObject.Bind();

            _gl.GetBufferParameter(BufferTargetARB.ArrayBuffer, BufferPNameARB.Size, out int bufferSize);

            if (bufferSize >= models.Length * sizeof(Matrix4x4))
            {
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, new ReadOnlySpan<Matrix4x4>(models));
            }
            else
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, new ReadOnlySpan<Matrix4x4>(models), BufferUsageARB.DynamicDraw);
            }

            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);

            GLMaterial.Scope[] materialScopes = ArrayPool<GLMaterial.Scope>.Shared.Rent(target.Materials.Length);
            for (var n = 0; n < target.Materials.Length; n++)
            {
                GLMaterial material = target.Materials[n];
                ShaderProgram shader = material.ShaderProgram;
                
                materialScopes[n] = material.Use();
                shader.SetUniform("view", view);
                shader.SetUniform("projection", projection);
                shader.SetUniform("uCameraPos", new Vector3(view.M41, view.M42, view.M43));
                shaderActivationCallback(shader);
            }

            target.VertexArrayObject.Bind();

            _gl.Set(EnableCap.DepthTest, !target.RenderOptions.IgnoreDepth);
            _gl.Set(EnableCap.CullFace, !target.RenderOptions.DoubleFaced);
            _gl.PolygonMode(TriangleFace.FrontAndBack, _renderSettings.Wireframe || target.RenderOptions.Wireframe ? PolygonMode.Line : PolygonMode.Fill);
            _gl.DrawElementsInstanced(PrimitiveType.Triangles, (uint)target.VertexArrayObject.ElementBufferObject.Length, DrawElementsType.UnsignedInt, (void*)0, (uint)models.Length);
            drawCalls++;

            for (var n = 0; n < target.Materials.Length; n++)
            {
                materialScopes[n].Dispose();
            }
            ArrayPool<GLMaterial.Scope>.Shared.Return(materialScopes);
        }

        return drawCalls;
    }
}