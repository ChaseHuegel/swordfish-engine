using System.Numerics;
using Silk.NET.OpenGL;
using Swordfish.Graphics.SilkNET.OpenGL.Util;
using Swordfish.Library.Collections;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Util;
using Swordfish.Settings;

namespace Swordfish.Graphics.SilkNET.OpenGL.Pipelines;

internal sealed class DeferredRenderingPipeline<TRenderStage> : RenderPipeline<TRenderStage>
    where TRenderStage : IRenderStage
{
    private readonly GL _gl;
    private readonly RenderSettings _renderSettings;
    private readonly IWindowContext _windowContext;
    
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
    
    public DeferredRenderingPipeline(
        in TRenderStage[] renderStages,
        in GL gl,
        in RenderSettings renderSettings,
        in IWindowContext windowContext,
        in IAssetDatabase<Shader> shaderDatabase,
        in GLContext glContext
    ) : base(renderStages) {
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
    
    public override void PreRender(double delta, RenderScene renderScene)
    {
        AntiAliasing antiAliasing = _renderSettings.AntiAliasing.Get();
        _gl.Set(EnableCap.Multisample, antiAliasing == AntiAliasing.MSAA);

        _gl.ClearColor(0f, 0f, 0f, 1f);
        
        //  Render to the gbuffer
        _gBuffer.Bind();
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
    }

    public override void PostRender(double delta, RenderScene renderScene)
    {
        _gBuffer.Unbind();
        
        //  Render the gbuffer
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        using (_screenShader.Use())
        {
            _screenShader.SetUniform("gPosition", 0);
            _screenShader.SetUniform("gNormal", 1);
            _screenShader.SetUniform("gColor", 2);
            Vector3 viewLightDirection = Vector3.Normalize(Vector3.TransformNormal(Vector3.Normalize(new Vector3(1, 1, 1f)), renderScene.View));
            _screenShader.SetUniform("viewLightDirection", viewLightDirection);
            _gBuffer.Activate();
            
            //  Draw the screen quad
            _gl.Set(EnableCap.CullFace, false);
            _screenVAO.Bind();
            _gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
            _screenVAO.Unbind();
            
            //  Copy the gbuffer's depth to the default framebuffer
            _gl.BindFramebuffer(GLEnum.ReadFramebuffer, _gBuffer.Handle);
            _gl.BindFramebuffer(GLEnum.DrawFramebuffer, 0);
            _gl.BlitFramebuffer(0, 0, (int)_windowContext.Resolution.X, (int)_windowContext.Resolution.Y, 0, 0, (int)_windowContext.Resolution.X, (int)_windowContext.Resolution.Y, ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest);
            _gl.BindFramebuffer(GLEnum.ReadFramebuffer, 0);
        }
    }

    protected override void ShaderActivationCallback(ShaderProgram shader)
    {
    }
}