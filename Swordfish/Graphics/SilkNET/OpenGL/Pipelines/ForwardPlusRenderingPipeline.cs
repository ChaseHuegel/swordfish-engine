using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.OpenGL;
using Swordfish.ECS;
using Swordfish.Graphics.SilkNET.OpenGL.Util;
using Swordfish.Library.Collections;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Extensions;
using Swordfish.Library.Util;
using Swordfish.Settings;

namespace Swordfish.Graphics.SilkNET.OpenGL.Pipelines;

internal sealed unsafe class ForwardPlusRenderingPipeline<TRenderStage> : RenderPipeline<TRenderStage>, IEntitySystem
    where TRenderStage : IRenderStage
{
    private const int TILE_WIDTH = 16;
    private const int TILE_HEIGHT = 16;
    private const int MAX_LIGHTS = 1024;
    private const int MAX_LIGHTS_PER_TILE = 1024;
    
    private readonly GL _gl;
    private readonly RenderSettings _renderSettings;
    
    private readonly ShaderProgram _depthShader;
    private readonly ShaderProgram _computeShader;
    private readonly ShaderProgram _ssaoShader;
    private readonly ShaderProgram _skyboxShader;
    private readonly ShaderProgram _blurShader;
    private readonly ShaderProgram _bloomShader;
    
    private uint _screenWidth;
    private uint _screenHeight;
    private uint _screenHalfWidth;
    private uint _screenHalfHeight;
    
    private int _numTilesX;
    private int _numTilesY;
    private int _numTiles;
    
    private readonly TexImage2D _preDepthTex;
    private readonly FramebufferObject _preDepthFBO;
    
    private readonly TexImage2D _depthTex;
    private readonly FramebufferObject _depthFBO;
    
    private readonly TexImage2D _ssaoTex;
    private readonly FramebufferObject _ssaoFBO;
    
    private readonly uint _renderFBO;
    private readonly uint _colorRBO;
    private readonly uint _bloomRBO;
    private readonly uint _depthStencilRBO;

    private readonly TexImage2D _blurTex;
    private readonly FramebufferObject _blurFBO;
    
    private readonly TexImage2D _screenTex;
    private readonly FramebufferObject _screenFBO;
    
    private readonly BufferObject<GPULight> _lightsSSBO;
    private readonly BufferObject<uint> _tileIndicesSSBO;
    private readonly BufferObject<uint> _tileCountsSSBO;
    
    private readonly BufferObject<float> _screenVBO;
    private readonly VertexArrayObject<float> _screenVAO;

    private readonly DrawBufferMode[] _drawBuffers = [DrawBufferMode.ColorAttachment0, DrawBufferMode.ColorAttachment1];
    private readonly DoubleList<GPULight> _lights = new();
    private readonly Vector3 _ambientLight = Color.FromArgb(20, 21, 37).ToVector3();
    
    private readonly float[] _quadVertices =
    [
        //  x, y, z, u, v
        -1.0f,  1.0f, 0.0f, 0.0f, 1.0f,
        -1.0f, -1.0f, 0.0f, 0.0f, 0.0f,
        1.0f,  1.0f, 0.0f, 1.0f, 1.0f,
        1.0f, -1.0f, 0.0f, 1.0f, 0.0f,
    ];
    
    public ForwardPlusRenderingPipeline(
        in TRenderStage[] renderStages,
        in GL gl,
        in RenderSettings renderSettings,
        in IWindowContext windowContext,
        in IAssetDatabase<Shader> shaderDatabase,
        in GLContext glContext
    ) : base(renderStages) {
        _gl = gl;
        _renderSettings = renderSettings;
        
        Shader depthShader = GetShaderOrDie(shaderDatabase, name: "forward/depth");
        Shader computeShader = GetShaderOrDie(shaderDatabase, name:  "forward/cull_lights");
        Shader ssaoShader = GetShaderOrDie(shaderDatabase, name: "forward/ssao");
        Shader skyboxShader = GetShaderOrDie(shaderDatabase, name: "skybox");
        Shader blurShader = GetShaderOrDie(shaderDatabase, name: "blur_gaussian");
        Shader bloomShader = GetShaderOrDie(shaderDatabase, name: "bloom");
        
        _depthShader = depthShader.CreateProgram(glContext);
        _computeShader = computeShader.CreateProgram(glContext);
        _ssaoShader = ssaoShader.CreateProgram(glContext);
        _skyboxShader = skyboxShader.CreateProgram(glContext);
        _blurShader = blurShader.CreateProgram(glContext);
        _bloomShader = bloomShader.CreateProgram(glContext);
        
        _screenWidth = (uint)windowContext.Resolution.X;
        _screenHeight = (uint)windowContext.Resolution.Y;
        _screenHalfWidth = _screenWidth / 2;
        _screenHalfHeight = _screenHeight / 2;
        
        _numTilesX = (int)(_screenWidth + TILE_WIDTH - 1) / TILE_WIDTH;
        _numTilesY = (int)(_screenHeight + TILE_HEIGHT - 1) / TILE_HEIGHT;
        _numTiles = _numTilesX * _numTilesY;
        
        _preDepthTex = new TexImage2D(_gl, name: "prepass_depth", pixels: null, _screenHalfWidth, _screenHalfHeight, TextureFormat.Depth24f, TextureParams.ClampNearest);
        _preDepthFBO = new FramebufferObject(_gl, name: "prepass_depth", _preDepthTex, FramebufferAttachment.DepthAttachment);
        
        _depthTex = new TexImage2D(_gl, name: "depth", pixels: null, _screenWidth, _screenHeight, TextureFormat.Depth24f, TextureParams.ClampNearest);
        _depthFBO = new FramebufferObject(_gl, name: "depth", _preDepthTex, FramebufferAttachment.DepthAttachment);
        
        _ssaoTex = new TexImage2D(_gl, name: "ssao", pixels: null, _screenHalfWidth, _screenHalfHeight, TextureFormat.R32f, TextureParams.ClampLinear);
        _ssaoFBO = new FramebufferObject(_gl, name: "ssao", _preDepthTex, FramebufferAttachment.ColorAttachment0);
        
        _blurTex = new TexImage2D(_gl, name: "blur", pixels: null, _screenWidth, _screenHeight, TextureFormat.Rgb16f, TextureParams.ClampLinear);
        _blurFBO = new FramebufferObject(_gl, name: "blur", _preDepthTex, FramebufferAttachment.ColorAttachment0);
        
        _screenTex = new TexImage2D(_gl, name: "screen", pixels: null, _screenWidth, _screenHeight, TextureFormat.Rgb16f, TextureParams.ClampLinear);
        _screenFBO = new FramebufferObject(_gl, name: "screen", _preDepthTex, FramebufferAttachment.ColorAttachment0);
        
        _screenVBO = new BufferObject<float>(_gl, _quadVertices, BufferTargetARB.ArrayBuffer);
        _screenVAO = new VertexArrayObject<float>(_gl, _screenVBO);
        _screenVAO.SetVertexAttributePointer(index: 0, count: 3, type: VertexAttribPointerType.Float, stride: 5 * sizeof(float), offset: 0);
        _screenVAO.SetVertexAttributePointer(index: 1, count: 2, type: VertexAttribPointerType.Float, stride: 5 * sizeof(float), offset: 3 * sizeof(float));

        _lightsSSBO = new BufferObject<GPULight>(_gl, Span<GPULight>.Empty, BufferTargetARB.ShaderStorageBuffer, BufferUsageARB.DynamicDraw, index: 0);
        _tileIndicesSSBO = new BufferObject<uint>(_gl, Span<uint>.Empty, BufferTargetARB.ShaderStorageBuffer, BufferUsageARB.DynamicDraw, index: 1);
        _tileCountsSSBO = new BufferObject<uint>(_gl, Span<uint>.Empty, BufferTargetARB.ShaderStorageBuffer, BufferUsageARB.DynamicDraw, index: 2);
        
        //  TODO renderbuffer support in the framebuffer object
        _renderFBO = gl.GenFramebuffer();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _renderFBO);
        gl.DrawBuffers(2, _drawBuffers);
        _colorRBO = gl.GenRenderbuffer();
        gl.BindRenderbuffer(GLEnum.Renderbuffer, _colorRBO);
        gl.RenderbufferStorageMultisample(GLEnum.Renderbuffer, samples: 4, InternalFormat.Rgba16f, _screenWidth, _screenHeight);
        gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, _colorRBO);
        _bloomRBO = gl.GenRenderbuffer();
        gl.BindRenderbuffer(GLEnum.Renderbuffer, _bloomRBO);
        gl.RenderbufferStorageMultisample(GLEnum.Renderbuffer, samples: 4, InternalFormat.Rgba8, _screenWidth, _screenHeight);
        gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, RenderbufferTarget.Renderbuffer, _bloomRBO);
        _depthStencilRBO = gl.GenRenderbuffer();
        gl.BindRenderbuffer(GLEnum.Renderbuffer, _depthStencilRBO);
        gl.RenderbufferStorageMultisample(GLEnum.Renderbuffer, samples: 4, InternalFormat.Depth24Stencil8, _screenWidth, _screenHeight);
        gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, _depthStencilRBO);
        GLEnum status = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        gl.BindRenderbuffer(GLEnum.Renderbuffer, 0);
        if (status != GLEnum.FramebufferComplete)
        {
            throw new FatalAlertException("Forward+ render framebuffer is incomplete.");
        }
        
        windowContext.Resized += OnWindowResized;
    }

    private static Shader GetShaderOrDie(IAssetDatabase<Shader> shaderDatabase, string name)
    {
        Result<Shader> depthShader = shaderDatabase.Get(name);
        if (!depthShader)
        {
            throw new FatalAlertException($"Failed to find the forward+ renderer's shader \"{name}\".");
        }

        return depthShader;
    }

    public void Tick(float delta, DataStore store)
    {
        lock (_lights)
        {
            _lights.Clear();
            store.Query<TransformComponent, LightComponent>(0f, LightQuery);
        }
    }

    private void LightQuery(float f, DataStore store, int entity, ref TransformComponent transform, ref LightComponent light)
    {
        var posRadius = new Vector4(transform.Position.X, transform.Position.Y, transform.Position.Z, light.Radius);
        var colorIntensity = new Vector4(light.Color.X, light.Color.Y, light.Color.Z, light.Size);
        _lights.Write(new GPULight(posRadius, colorIntensity));
    }

    private void OnWindowResized(Vector2 size)
    {
        _screenWidth = (uint)size.X;
        _screenHeight = (uint)size.Y;
        _screenHalfWidth = _screenWidth / 2;
        _screenHalfHeight = _screenHeight / 2;
        
        _numTilesX = (int)(_screenWidth + TILE_WIDTH - 1) / TILE_WIDTH;
        _numTilesY = (int)(_screenHeight + TILE_HEIGHT - 1) / TILE_HEIGHT;
        _numTiles = _numTilesX * _numTilesY;
        
        _gl.BindTexture(TextureTarget.Texture2D, _preDepthTex);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.DepthComponent24, _screenHalfWidth, _screenHalfHeight, 0, GLEnum.DepthComponent, GLEnum.Float, null);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        
        _gl.BindTexture(TextureTarget.Texture2D, _depthTex);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.DepthComponent24, _screenWidth, _screenHeight, 0, GLEnum.DepthComponent, GLEnum.Float, null);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        
        _gl.BindTexture(TextureTarget.Texture2D, _ssaoTex);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.R32f, _screenHalfWidth, _screenHalfHeight, 0, PixelFormat.Red, PixelType.Float, null);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        
        _gl.BindTexture(TextureTarget.Texture2D, _blurTex);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgb16f, _screenWidth, _screenHeight, 0, PixelFormat.Rgb, PixelType.Float, null);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        
        _gl.BindTexture(TextureTarget.Texture2D, _screenTex);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba16f, _screenWidth, _screenHeight, 0, PixelFormat.Rgba, PixelType.Float, null);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        
        _gl.BindTexture(TextureTarget.Texture2D, 0);
        
        _gl.BindRenderbuffer(GLEnum.Renderbuffer, _colorRBO);
        _gl.RenderbufferStorageMultisample(GLEnum.Renderbuffer, samples: 4, InternalFormat.Rgba16f, _screenWidth, _screenHeight);
        _gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, _colorRBO);
        
        _gl.BindRenderbuffer(GLEnum.Renderbuffer, _bloomRBO);
        _gl.RenderbufferStorageMultisample(GLEnum.Renderbuffer, samples: 4, InternalFormat.Rgba8, _screenWidth, _screenHeight);
        _gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, RenderbufferTarget.Renderbuffer, _bloomRBO);
        
        _gl.BindRenderbuffer(GLEnum.Renderbuffer, _depthStencilRBO);
        _gl.RenderbufferStorageMultisample(GLEnum.Renderbuffer, samples: 4, InternalFormat.Depth24Stencil8, _screenWidth, _screenHeight);
        _gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, _depthStencilRBO);
        
        _gl.BindRenderbuffer(GLEnum.Renderbuffer, 0);
        
        _gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, _tileIndicesSSBO);
        int indicesCount = _numTiles * MAX_LIGHTS_PER_TILE;
        _gl.BufferData(BufferTargetARB.ShaderStorageBuffer, (nuint)(indicesCount * sizeof(uint)), null, BufferUsageARB.DynamicDraw);
        _gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 1, _tileIndicesSSBO);
        
        _gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, _tileCountsSSBO);
        _gl.BufferData(BufferTargetARB.ShaderStorageBuffer, (nuint)(_numTiles * sizeof(uint)), null, BufferUsageARB.DynamicDraw);
        _gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 2, _tileCountsSSBO);
        
        _gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, 0);
    }

    public override void PreRender(double delta, Matrix4x4 view, Matrix4x4 projection)
    {
        AntiAliasing antiAliasing = _renderSettings.AntiAliasing.Get();
        _gl.Set(EnableCap.Multisample, antiAliasing == AntiAliasing.MSAA);
        
        float near = projection.M34 / (projection.M33 - 1.0f);
        float far = projection.M34 / (projection.M33 + 1.0f);
        Matrix4x4.Invert(projection, out Matrix4x4 inverseProjection);
        
        // Depth pre-pass
        using (_preDepthFBO.Use())
        {
            _gl.DepthMask(true);
            _gl.Clear((uint)ClearBufferMask.DepthBufferBit);
            _gl.Enable(GLEnum.DepthTest);
            
            _depthShader.Activate();
            _depthShader.SetUniform("view", view);
            _depthShader.SetUniform("projection", projection);
            _depthShader.SetUniform("near", near);
            _depthShader.SetUniform("far", far);
            
            Draw(delta, view, projection, isDepthPass: true);
        }
        
        // SSAO pass
        using (_ssaoFBO.Use())
        {
            _gl.Clear(ClearBufferMask.ColorBufferBit);

            _ssaoShader.Activate();
            _preDepthTex.Activate();
            _gl.Uniform1(_gl.GetUniformLocation(_ssaoShader.Handle, "uDepthTex"), 0);
            _ssaoShader.SetUniform("uInvProj", inverseProjection);
            
            _screenVAO.Bind();
            _gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
            _screenVAO.Unbind();
        }
        
        // Depth full pass
        using (_depthFBO.Use()) 
        {
            _gl.DepthMask(true);
            _gl.Clear((uint)ClearBufferMask.DepthBufferBit);
            _gl.Enable(GLEnum.DepthTest);
            
            _depthShader.Activate();
            _depthShader.SetUniform("view", view);
            _depthShader.SetUniform("projection", projection);
            _depthShader.SetUniform("near", near);
            _depthShader.SetUniform("far", far);
            
            Draw(delta, view, projection, isDepthPass: false);
        }
        
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _gl.Viewport(0, 0, _screenWidth, _screenHeight);
        
        // Upload lights
        GPULight[] lights;
        lock (_lights)
        {
            lights = _lights.Read();
            _lights.Swap();
        }

        _gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, _lightsSSBO);
        int bytes = lights.Length * Marshal.SizeOf<GPULight>();
        fixed (GPULight* p = lights)
        {
            _gl.BufferSubData(BufferTargetARB.ShaderStorageBuffer, 0, (nuint)bytes, p);
        }
        
        // Clear tile counts
        _gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, _tileCountsSSBO);
        var zeros = new uint[_numTiles];
        fixed (uint* z = zeros) {
            _gl.BufferSubData(BufferTargetARB.ShaderStorageBuffer, 0, (nuint)(zeros.Length * sizeof(uint)), z);
        }
        
        //  Dispatch tile compute shader
        _computeShader.Activate();
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _depthTex);
        _gl.Uniform2(_gl.GetUniformLocation(_computeShader.Handle, "uScreenSize"), (int)_screenWidth, (int)_screenHeight);
        _gl.Uniform2(_gl.GetUniformLocation(_computeShader.Handle, "uTileSize"), TILE_WIDTH, TILE_HEIGHT);
        _gl.Uniform1(_gl.GetUniformLocation(_computeShader.Handle, "uNumLights"), lights.Length);
        _gl.Uniform1(_gl.GetUniformLocation(_computeShader.Handle, "uMaxLightsPerTile"), MAX_LIGHTS_PER_TILE);
        _gl.Uniform1(_gl.GetUniformLocation(_computeShader.Handle, "uMaxLightViewDistance"), 1000f);
        _computeShader.SetUniform("uInvProj", inverseProjection);
        
        var groupsX = (uint)_numTilesX;
        var groupsY = (uint)_numTilesY;
        _gl.DispatchCompute(groupsX, groupsY, 1);
        _gl.MemoryBarrier( MemoryBarrierMask.ShaderStorageBarrierBit | MemoryBarrierMask.TextureUpdateBarrierBit);
        
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _renderFBO);
        _gl.Viewport(0, 0, _screenWidth, _screenHeight);
        _gl.ClearColor(0f, 0f, 0f, 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
        
        //  Skybox pass
        _gl.DrawBuffer(DrawBufferMode.ColorAttachment0); // Only render to the color buffer
        _gl.DepthMask(false);
        _skyboxShader.Activate();
        _skyboxShader.SetUniform("uRGB", _ambientLight);
        _screenVAO.Bind();
        _gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
        _screenVAO.Unbind();
        _gl.DepthMask(true);
        
        _gl.DrawBuffers(_drawBuffers);
    }

    public override void PostRender(double delta, Matrix4x4 view, Matrix4x4 projection)
    {
        //  Blit the render buffer to the back buffer
        _gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _renderFBO);
        _gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
        
        _gl.ReadBuffer(GLEnum.ColorAttachment0);
        _gl.DrawBuffer(GLEnum.Back);
        
        _gl.BlitFramebuffer(
            0, 0, (int)_screenWidth, (int)_screenHeight,
            0, 0, (int)_screenWidth, (int)_screenHeight,
            ClearBufferMask.ColorBufferBit,
            BlitFramebufferFilter.Linear
        );
        
        //  Bloom pre-pass
        //  Blit the bloom renderbuffer to the blur texture
        _gl.BindTexture(TextureTarget.Texture2D, _blurTex);
        _gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _renderFBO);
        _gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _blurFBO);
        _gl.ReadBuffer(GLEnum.ColorAttachment1);
        _gl.DrawBuffer(GLEnum.ColorAttachment0);
        
        _gl.BlitFramebuffer(
            0, 0, (int)_screenWidth, (int)_screenHeight,
            0, 0, (int)_screenWidth, (int)_screenHeight,
            ClearBufferMask.ColorBufferBit,
            BlitFramebufferFilter.Linear
        );
        
        //  Blur the bloom texture
        _blurShader.Activate();
        _blurShader.SetUniform("texture0", 0);
        _gl.ActiveTexture(TextureUnit.Texture0);
        //  TODO Blur should be done with separate textures for
        //       the horizontal and vertical blurs. This creates artifacts,
        //       but they aren't too noticeable. since this is just for bloom.
        //       Using a single texture and single pass is a bit more performant.
        //       Whether one or two textures is used could be driven by quality settings.
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _blurFBO);
        _gl.BindTexture(TextureTarget.Texture2D, _blurTex);
        var horizontal = true;
        for (var i = 0; i <= 10; i++)
        {
            int horizontalInt = horizontal ? 0 : 1;
            horizontal = !horizontal;
            
            _blurShader.SetUniform("uHorizontal", horizontalInt);
            
            _screenVAO.Bind();
            _gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
            _screenVAO.Unbind();
        }
        
        //  Blit the bloom renderbuffer to the screen texture
        _gl.BindTexture(TextureTarget.Texture2D, _screenTex);
        _gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _renderFBO);
        _gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _screenFBO);
        _gl.ReadBuffer(GLEnum.ColorAttachment1);
        _gl.DrawBuffer(GLEnum.ColorAttachment0);
        
        _gl.BlitFramebuffer(
            0, 0, (int)_screenWidth, (int)_screenHeight,
            0, 0, (int)_screenWidth, (int)_screenHeight,
            ClearBufferMask.ColorBufferBit,
            BlitFramebufferFilter.Linear
        );
        
        //  Return to the back buffer
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        
        //  Render the bloom overlay
        _gl.DepthMask(false);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.One, BlendingFactor.One);
        _bloomShader.Activate();
        _bloomShader.SetUniform("texture0", 0);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _blurTex);
        _bloomShader.SetUniform("texture1", 1);
        _gl.ActiveTexture(TextureUnit.Texture1);
        _gl.BindTexture(TextureTarget.Texture2D, _screenTex);
        _screenVAO.Bind();
        _gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
        _screenVAO.Unbind();
        _gl.Disable(EnableCap.Blend);
        _gl.DepthMask(true);
    }
    
    protected override void ShaderActivationCallback(ShaderProgram shader)
    {
        _gl.Uniform2(_gl.GetUniformLocation(shader.Handle, "uScreenSize"), (int)_screenWidth, (int)_screenHeight);
        _gl.Uniform2(_gl.GetUniformLocation(shader.Handle, "uTileSize"), TILE_WIDTH, TILE_HEIGHT);
        _gl.Uniform1(_gl.GetUniformLocation(shader.Handle, "uMaxLightsPerTile"), MAX_LIGHTS_PER_TILE);
        shader.SetUniform("uAmbientLight", _ambientLight);
        
        _gl.ActiveTexture(TextureUnit.Texture5);
        _gl.BindTexture(TextureTarget.Texture2D, _ssaoTex);
        shader.SetUniform("uAO", 5);
        
        _gl.ActiveTexture(TextureUnit.Texture6);
        _gl.BindTexture(TextureTarget.Texture2D, _preDepthTex);
        shader.SetUniform("uPreDepth", 6);
        
        _gl.ActiveTexture(TextureUnit.Texture7);
        _gl.BindTexture(TextureTarget.Texture2D, _depthTex);
        shader.SetUniform("uDepth", 7);
    }
}