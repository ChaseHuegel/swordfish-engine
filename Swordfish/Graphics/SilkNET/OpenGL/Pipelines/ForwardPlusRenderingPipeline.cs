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
    private uint[] _emptyTileCounts;
    
    private readonly TexImage2D _preDepthTex;
    private readonly FramebufferObject _preDepthFBO;
    
    private readonly TexImage2D _depthTex;
    private readonly FramebufferObject _depthFBO;
    
    private readonly TexImage2D _ssaoTex;
    private readonly FramebufferObject _ssaoFBO;

    private readonly TexImage2D _blurTex;
    private readonly FramebufferObject _blurFBO;
    
    private readonly TexImage2D _screenTex;
    private readonly FramebufferObject _screenFBO;
    
    private readonly FramebufferObject _renderFBO;
    private readonly RenderbufferObject _colorRBO;
    private readonly RenderbufferObject _bloomRBO;
    private readonly RenderbufferObject _depthStencilRBO;
    
    private readonly BufferObject<GPULight> _lightsSSBO;
    private readonly BufferObject<uint> _tileIndicesSSBO;
    private readonly BufferObject<uint> _tileCountsSSBO;
    
    private readonly BufferObject<float> _screenVBO;
    private readonly VertexArrayObject<float> _screenVAO;

    private readonly DoubleList<GPULight> _lights = new();
    private readonly Vector3 _ambientLight = Color.FromArgb(20, 21, 37).ToVector3();
    private readonly DrawBufferMode[] _drawBuffers = [DrawBufferMode.ColorAttachment0, DrawBufferMode.ColorAttachment1];
    
    private readonly float[] _screenQuadVertices =
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
        _emptyTileCounts = new uint[_numTiles];
        
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
        
        _screenVBO = new BufferObject<float>(_gl, _screenQuadVertices, BufferTargetARB.ArrayBuffer);
        _screenVAO = new VertexArrayObject<float>(_gl, _screenVBO);
        _screenVAO.SetVertexAttributePointer(index: 0, count: 3, type: VertexAttribPointerType.Float, stride: 5 * sizeof(float), offset: 0);
        _screenVAO.SetVertexAttributePointer(index: 1, count: 2, type: VertexAttribPointerType.Float, stride: 5 * sizeof(float), offset: 3 * sizeof(float));
        
        _lightsSSBO = new BufferObject<GPULight>(_gl, size: MAX_LIGHTS, BufferTargetARB.ShaderStorageBuffer, BufferUsageARB.DynamicDraw, index: 0);
        _tileIndicesSSBO = new BufferObject<uint>(_gl, size: _numTiles * MAX_LIGHTS_PER_TILE, BufferTargetARB.ShaderStorageBuffer, BufferUsageARB.DynamicDraw, index: 1);
        _tileCountsSSBO = new BufferObject<uint>(_gl, size: _numTiles, BufferTargetARB.ShaderStorageBuffer, BufferUsageARB.DynamicDraw, index: 2);
        
        _colorRBO = new RenderbufferObject(_gl, name: "render_color", _screenWidth, _screenHeight, FramebufferAttachment.ColorAttachment0, InternalFormat.Rgba16f, samples: 4);
        _bloomRBO = new RenderbufferObject(_gl, name: "render_bloom", _screenWidth, _screenHeight, FramebufferAttachment.ColorAttachment1, InternalFormat.Rgba8, samples: 4);
        _depthStencilRBO = new RenderbufferObject(_gl, name: "render_DS", _screenWidth, _screenHeight, FramebufferAttachment.DepthStencilAttachment, InternalFormat.Depth24Stencil8, samples: 4);
        _renderFBO = new FramebufferObject(_gl, name: "render", _screenWidth, _screenHeight, _drawBuffers, renderBuffers: [_colorRBO, _bloomRBO, _depthStencilRBO]);
        
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
        _emptyTileCounts = new uint[_numTiles];

        _preDepthTex.UpdateData(_screenHalfWidth, _screenHalfHeight, pixels: null);
        _depthTex.UpdateData(_screenWidth, _screenHeight, pixels: null);
        _ssaoTex.UpdateData(_screenHalfWidth, _screenHalfHeight, pixels: null);
        _blurTex.UpdateData(_screenWidth, _screenHeight, pixels: null);
        _screenTex.UpdateData(_screenWidth, _screenHeight, pixels: null);
        
        _colorRBO.Resize(_screenWidth, _screenHeight);
        _bloomRBO.Resize(_screenWidth, _screenHeight);
        _depthStencilRBO.Resize(_screenWidth, _screenHeight);
        
        _tileIndicesSSBO.Resize(_numTiles * MAX_LIGHTS_PER_TILE);
        _tileCountsSSBO.Resize(_numTiles);
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
        using (_depthShader.Use())
        {
            _depthShader.SetUniform("view", view);
            _depthShader.SetUniform("projection", projection);
            _depthShader.SetUniform("near", near);
            _depthShader.SetUniform("far", far);
            
            _gl.DepthMask(true);
            _gl.Clear((uint)ClearBufferMask.DepthBufferBit);
            _gl.Enable(GLEnum.DepthTest);

            Draw(delta, view, projection, isDepthPass: true);
        }

        // SSAO pass
        using (_ssaoFBO.Use())
        using (_ssaoShader.Use())
        using (_preDepthTex.Activate(TextureUnit.Texture0))
        {
            _gl.Uniform1(_gl.GetUniformLocation(_ssaoShader.Handle, "uDepthTex"), 0);
            _ssaoShader.SetUniform("uInvProj", inverseProjection);
            
            _gl.Clear(ClearBufferMask.ColorBufferBit);

            _screenVAO.Bind();
            _gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
            _screenVAO.Unbind();
        }

        // Depth full pass
        using (_depthFBO.Use())
        using (_depthShader.Use())
        {
            _depthShader.SetUniform("view", view);
            _depthShader.SetUniform("projection", projection);
            _depthShader.SetUniform("near", near);
            _depthShader.SetUniform("far", far);
            
            _gl.DepthMask(true);
            _gl.Clear((uint)ClearBufferMask.DepthBufferBit);
            _gl.Enable(GLEnum.DepthTest);

            Draw(delta, view, projection, isDepthPass: false);
        }

        //  Reset to the back buffer
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _gl.Viewport(0, 0, _screenWidth, _screenHeight);

        // Upload lights
        GPULight[] lights;
        lock (_lights)
        {
            lights = _lights.Read();
            _lights.Swap();
        }
        _lightsSSBO.UpdateData(lights);

        // Clear tile counts
        _tileCountsSSBO.UpdateData(_emptyTileCounts);

        //  Dispatch tile compute shader
        using (_computeShader.Use())
        using (_depthTex.Activate(TextureUnit.Texture0))
        {
            _computeShader.SetUniform("uScreenSize", (int)_screenWidth, (int)_screenHeight);
            _computeShader.SetUniform("uTileSize", TILE_WIDTH, TILE_HEIGHT);
            _computeShader.SetUniform("uNumLights", lights.Length);
            _computeShader.SetUniform("uMaxLightsPerTile", MAX_LIGHTS_PER_TILE);
            _computeShader.SetUniform("uMaxLightViewDistance", 1000f);
            _computeShader.SetUniform("uInvProj", inverseProjection);
            
            var groupsX = (uint)_numTilesX;
            var groupsY = (uint)_numTilesY;
            const int groupsZ = 1;
            _gl.DispatchCompute(groupsX, groupsY, groupsZ);
            _gl.MemoryBarrier(MemoryBarrierMask.ShaderStorageBarrierBit | MemoryBarrierMask.TextureUpdateBarrierBit);
        }

        //  Clear and begin drawing to the render buffer
        _renderFBO.Bind();
        _gl.ClearColor(0f, 0f, 0f, 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

        //  Skybox pass
        using (_skyboxShader.Use())
        {
            _skyboxShader.SetUniform("uRGB", _ambientLight);

            _gl.DrawBuffer(DrawBufferMode.ColorAttachment0);
            _gl.DepthMask(false);
            
            _screenVAO.Bind();
            _gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
            _screenVAO.Unbind();
            
            _gl.DepthMask(true);
        }
        
        //  Reset draw buffers
        _gl.DrawBuffers(_drawBuffers);
    }

    public override void PostRender(double delta, Matrix4x4 view, Matrix4x4 projection)
    {
        //  Blit the render buffer to the back buffer
        using (_renderFBO.Use(FramebufferTarget.ReadFramebuffer, DrawBufferMode.ColorAttachment0))
        {
            _gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
            _gl.DrawBuffer(GLEnum.Back);
            
            _gl.BlitFramebuffer(
                0, 0, (int)_screenWidth, (int)_screenHeight,
                0, 0, (int)_screenWidth, (int)_screenHeight,
                ClearBufferMask.ColorBufferBit,
                BlitFramebufferFilter.Linear
            );
        }
        
        //  Bloom pre-pass
        //  Blit the bloom renderbuffer to the blur texture
        using (_blurTex.Use()) 
        using (_renderFBO.Use(FramebufferTarget.ReadFramebuffer, DrawBufferMode.ColorAttachment1))
        using (_blurFBO.Use(FramebufferTarget.DrawFramebuffer, DrawBufferMode.ColorAttachment0))
        {
            _gl.BlitFramebuffer(
                0, 0, (int)_screenWidth, (int)_screenHeight,
                0, 0, (int)_screenWidth, (int)_screenHeight,
                ClearBufferMask.ColorBufferBit,
                BlitFramebufferFilter.Linear
            );
        }
        
        //  Blur the bloom texture
        //  TODO Blur should be done with separate textures for
        //       the horizontal and vertical blurs. This creates artifacts,
        //       but they aren't too noticeable. since this is just for bloom.
        //       Using a single texture and single pass is a bit more performant.
        //       Whether one or two textures is used could be driven by quality settings.
        using (_blurFBO.Use())
        using (_blurShader.Use())
        using (_blurTex.Activate(TextureUnit.Texture0))
        {
            _blurShader.SetUniform("texture0", 0);
            
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
        }
        
        //  Blit the bloom renderbuffer to the screen texture
        using (_screenTex.Use())
        using (_renderFBO.Use(FramebufferTarget.ReadFramebuffer, DrawBufferMode.ColorAttachment1))
        using (_screenFBO.Use(FramebufferTarget.DrawFramebuffer, DrawBufferMode.ColorAttachment0))
        {
            _gl.BlitFramebuffer(
                srcX0: 0, srcY0: 0, srcX1: (int)_screenWidth, srcY1: (int)_screenHeight,
                dstX0: 0, dstY0: 0, dstX1: (int)_screenWidth, dstY1: (int)_screenHeight,
                mask: ClearBufferMask.ColorBufferBit,
                filter: BlitFramebufferFilter.Linear
            );
        }

        //  Render the bloom overlay
        using (_bloomShader.Use())
        using (_blurTex.Activate(TextureUnit.Texture0))
        using (_screenTex.Activate(TextureUnit.Texture1))
        {
            _bloomShader.SetUniform("texture0", 0);
            _bloomShader.SetUniform("texture1", 1);

            _gl.DepthMask(false);
            _gl.Enable(EnableCap.Blend);
            _gl.BlendFunc(BlendingFactor.One, BlendingFactor.One);

            _screenVAO.Bind();
            _gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
            _screenVAO.Unbind();

            _gl.Disable(EnableCap.Blend);
            _gl.DepthMask(true);
        }
    }
    
    protected override void ShaderActivationCallback(ShaderProgram shader)
    {
        shader.SetUniform("uScreenSize", (int)_screenWidth, (int)_screenHeight);
        shader.SetUniform("uTileSize", TILE_WIDTH, TILE_HEIGHT);
        shader.SetUniform("uMaxLightsPerTile", MAX_LIGHTS_PER_TILE);
        shader.SetUniform("uAmbientLight", _ambientLight);
        
        _ssaoTex.Activate(TextureUnit.Texture5);
        shader.SetUniform("uAO", 5);
        
        _preDepthTex.Activate(TextureUnit.Texture6);
        shader.SetUniform("uPreDepth", 6);
        
        _depthTex.Activate(TextureUnit.Texture7);
        shader.SetUniform("uDepth", 7);
    }
}