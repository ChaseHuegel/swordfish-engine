using System.Numerics;
using Silk.NET.OpenGL;
using Swordfish.Graphics.SilkNET.OpenGL.Util;
using Swordfish.Library.Collections;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Extensions;
using Swordfish.Library.Util;
using Swordfish.Settings;

namespace Swordfish.Graphics.SilkNET.OpenGL.Renderers;

internal unsafe class DeferredRenderer : IRenderStage
{
    private readonly GL _gl;
    private readonly RenderSettings _renderSettings;
    private readonly IWindowContext _windowContext;
    
    private readonly Dictionary<GLRenderTarget, List<Matrix4x4>> _instances = [];
    private readonly Dictionary<GLRenderTarget, List<Matrix4x4>> _transparentInstances = [];
    private LockedList<GLRenderTarget>? _renderTargets;

    private readonly GBuffer _gBuffer;
    private readonly VertexArrayObject<float> _screenVAO;
    private readonly ShaderProgram _screenShader;

    private readonly float[] _quadVertices =
    [
        //  x, y, z, u, v
        -1.0f,  1.0f, 0.0f, 0.0f, 1.0f,
        -1.0f, -1.0f, 0.0f, 0.0f, 0.0f,
         1.0f,  1.0f, 0.0f, 1.0f, 1.0f,
         1.0f, -1.0f, 0.0f, 1.0f, 0.0f,
    ];

    public DeferredRenderer(
        in GL gl,
        in RenderSettings renderSettings,
        in IWindowContext windowContext,
        in IAssetDatabase<Shader> shaderDatabase,
        in GLContext glContext
    ) {
        _gl = gl;
        _renderSettings = renderSettings;
        _windowContext = windowContext;
        _gBuffer = new GBuffer(_gl, (uint)_windowContext.Resolution.X, (uint)_windowContext.Resolution.Y);

        var quadVBO = new BufferObject<float>(_gl, _quadVertices, BufferTargetARB.ArrayBuffer);
        _screenVAO = new VertexArrayObject<float>(_gl, quadVBO);
        _screenVAO.SetVertexAttributePointer(0, 3, VertexAttribPointerType.Float, 5 * sizeof(float), 0);
        _screenVAO.SetVertexAttributePointer(1, 2, VertexAttribPointerType.Float, 5 * sizeof(float), 3 * sizeof(float));
        
        const string shaderName = "deferred_lighting";
        Result<Shader> screenShader = shaderDatabase.Get(shaderName);
        if (!screenShader)
        {
            throw new FatalAlertException($"Failed to load the deferred renderer's shader \"{shaderName}\".");
        }
        
        _screenShader = screenShader.Value.CreateProgram(glContext);
    }

    public void Initialize(IRenderContext renderContext)
    {
        if (renderContext is not GLRenderContext glRenderContext)
        {
            throw new NotSupportedException($"{nameof(GLInstancedRenderer)} only supports an OpenGL {nameof(IRenderContext)}.");
        }

        //  TODO this is bad
        _renderTargets = glRenderContext.RenderTargets;
    }

    public void PreRender(double delta, Matrix4x4 view, Matrix4x4 projection)
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

    public int Render(double delta, Matrix4x4 view, Matrix4x4 projection)
    {
        if (_renderTargets == null)
        {
            throw new InvalidOperationException($"{nameof(Render)} was called without initializing a valid render targets collection.");
        }

        if (_renderTargets.Count == 0 || _renderSettings.HideMeshes)
        {
            return 0;
        }

        _gl.ClearColor(0f, 0f, 0f, 1f);
        
        //  Render geometry to the gbuffer
        _gBuffer.Bind();
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        var drawCalls = 0;
        drawCalls += Draw(view, projection, _instances, sort: false);
        drawCalls += Draw(view, projection, _transparentInstances, sort: true);
        _gBuffer.Unbind();
        
        //  Render the gbuffer
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        _screenShader.Activate();
        _screenShader.SetUniform("gPosition", 0);
        _screenShader.SetUniform("gNormal", 1);
        _screenShader.SetUniform("gColor", 2);
        Vector3 viewLightDirection = Vector3.Normalize(Vector3.TransformNormal(Vector3.Normalize(new Vector3(1, 1, 1f)), view));
        _screenShader.SetUniform("viewLightDirection", viewLightDirection);
        _gBuffer.Activate();
        //  Draw the quad
        _gl.Set(EnableCap.CullFace, false);
        _screenVAO.Bind();
        _gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
        _screenVAO.Unbind();
        
        //  Copy the gbuffer's depth to the default framebuffer
        _gl.BindFramebuffer(GLEnum.ReadFramebuffer, _gBuffer.Handle);
        _gl.BindFramebuffer(GLEnum.DrawFramebuffer, 0);
        _gl.BlitFramebuffer(0, 0, (int)_windowContext.Resolution.X, (int)_windowContext.Resolution.Y, 0, 0, (int)_windowContext.Resolution.X, (int)_windowContext.Resolution.Y, ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest);
        _gl.BindFramebuffer(GLEnum.Framebuffer, 0);
        
        return drawCalls;
    }

    private int Draw(Matrix4x4 view, Matrix4x4 projection, Dictionary<GLRenderTarget, List<Matrix4x4>> instances, bool sort)
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

            for (var n = 0; n < target.Materials.Length; n++)
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
            _gl.PolygonMode(TriangleFace.FrontAndBack, _renderSettings.Wireframe || target.RenderOptions.Wireframe ? PolygonMode.Line : PolygonMode.Fill);
            _gl.DrawElementsInstanced(PrimitiveType.Triangles, (uint)target.VertexArrayObject.ElementBufferObject.Length, DrawElementsType.UnsignedInt, (void*)0, (uint)models.Length);
            drawCalls++;
        }

        return drawCalls;
    }
}