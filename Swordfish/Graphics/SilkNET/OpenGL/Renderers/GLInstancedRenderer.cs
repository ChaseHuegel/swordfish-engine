using System.Collections.Concurrent;
using System.Numerics;
using Silk.NET.OpenGL;
using Swordfish.Library.Types;
using Swordfish.Settings;

namespace Swordfish.Graphics.SilkNET.OpenGL.Renderers;

internal unsafe class GLInstancedRenderer : IRenderStage
{
    private ConcurrentDictionary<GLRenderTarget, ConcurrentBag<Matrix4x4>> _instances = [];

    private readonly GL _gl;
    private readonly RenderSettings _renderSettings;
    private ConcurrentBag<GLRenderTarget>? _renderTargets;

    public GLInstancedRenderer(GL gl, RenderSettings renderSettings)
    {
        _gl = gl;
        _renderSettings = renderSettings;
    }

    public void Initialize(IRenderContext renderContext)
    {
        if (renderContext is not GLRenderContext gLRenderContext)
        {
            throw new NotSupportedException($"{nameof(GLInstancedRenderer)} only supports an OpenGL {nameof(IRenderContext)}.");
        }

        _renderTargets = gLRenderContext.RenderTargets;
    }

    public void PreRender(double delta, Matrix4x4 view, Matrix4x4 projection)
    {
        if (_renderTargets == null)
        {
            throw new InvalidOperationException($"{nameof(PreRender)} was called without initializing a valid render targets collection.");
        }

        _instances.Clear();
        foreach (GLRenderTarget renderTarget in _renderTargets)
        {
            if (!_instances.TryGetValue(renderTarget, out ConcurrentBag<Matrix4x4>? matrices))
            {
                matrices = [];
                _instances.TryAdd(renderTarget, matrices);
            }

            Transform transform = renderTarget.Transform;
            matrices.Add(transform.ToMatrix4x4());
        }
    }

    public int Render(double delta, Matrix4x4 view, Matrix4x4 projection)
    {
        if (_renderTargets == null)
        {
            throw new InvalidOperationException($"{nameof(Render)} was called without initializing a valid render targets collection.");
        }

        if (_renderTargets.IsEmpty || _renderSettings.HideMeshes)
        {
            return 0;
        }

        int drawCalls = 0;
        foreach (KeyValuePair<GLRenderTarget, ConcurrentBag<Matrix4x4>> instance in _instances)
        {
            GLRenderTarget target = instance.Key;
            Matrix4x4[] models = [.. instance.Value];

            target.ModelsBufferObject.Bind();

            _gl.GetBufferParameter(BufferTargetARB.ArrayBuffer, BufferPNameARB.Size, out int bufferSize);

            if (bufferSize >= models.Length * sizeof(Matrix4x4))
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, new ReadOnlySpan<Matrix4x4>(models));
            else
                _gl.BufferData(BufferTargetARB.ArrayBuffer, new ReadOnlySpan<Matrix4x4>(models), BufferUsageARB.DynamicDraw);

            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);

            for (int n = 0; n < target.Materials.Length; n++)
            {
                GLMaterial material = target.Materials[n];
                ShaderProgram shader = material.ShaderProgram;
                material.Use();

                shader.SetUniform("view", view);
                shader.SetUniform("projection", projection);
            }

            target.VertexArrayObject.Bind();

            _gl.Set(EnableCap.DepthTest, !target.RenderOptions.IgnoreDepth);
            _gl.Set(EnableCap.CullFace, !target.RenderOptions.DoubleFaced);
            _gl.PolygonMode(MaterialFace.FrontAndBack, _renderSettings.Wireframe || target.RenderOptions.Wireframe ? PolygonMode.Line : PolygonMode.Fill);
            _gl.DrawElementsInstanced(PrimitiveType.Triangles, (uint)target.VertexArrayObject.ElementBufferObject.Length, DrawElementsType.UnsignedInt, (void*)0, (uint)models.Length);
            drawCalls++;
        }

        return drawCalls;
    }
}