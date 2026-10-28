using System.Numerics;
using Silk.NET.OpenGL;
using Swordfish.Diagnostics.SilkNET.OpenGL;
using Swordfish.ECS;
using Swordfish.Graphics.SilkNET.OpenGL.Util;
using Swordfish.Library.Collections;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Extensions;
using Swordfish.Library.Types;
using Swordfish.Library.Util;
using Swordfish.Settings;

namespace Swordfish.Graphics.SilkNET.OpenGL.Pipelines;

internal sealed unsafe class ForwardPlusRenderingPipeline<TRenderStage> : RenderPipeline<TRenderStage>, IEntitySystem, IDisposable
    where TRenderStage : IRenderStage
{
    private const int TILE_WIDTH = 16;
    private const int TILE_HEIGHT = 16;
    private const int MAX_LIGHTS = 1024;
    private const int MAX_LIGHTS_PER_TILE = 256;
    
    private readonly GL _gl;
    private readonly GLDebug _glDebug;
    private readonly RenderSettings _renderSettings;
    private readonly IWindowContext _windowContext;
    private readonly GLContext _glContext;

    private readonly ShaderProgram _depthShader;
    private readonly ShaderProgram _depthCutoutShader;
    private readonly ShaderProgram _depthCutoutArrayShader;
    private readonly ShaderProgram _computeShader;
    private readonly ShaderProgram _ssaoShader;
    private readonly ShaderProgram _skyboxShader;
    private readonly ShaderProgram _cubemapShader;
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
    
    private readonly BufferObject<float> _skyboxVBO;
    private readonly VertexArrayObject<float> _skyboxVAO;
    private TexCubemap? _skyboxTex;

    private readonly DoubleList<GPULight> _lightsBuffer = new();
    private readonly DrawBufferMode[] _drawBuffers = [DrawBufferMode.ColorAttachment0, DrawBufferMode.ColorAttachment1];
    
    private uint _csmFBO;
    private TexImage3D? _csmTexture;
    private uint _csmResolution;
    private int _csmCascadeCount;
    private ShadowFilterMode _csmFilterMode;
    private readonly Matrix4x4[] _cascadeViewProj = new Matrix4x4[4];
    private Vector4 _cascadeSplits;
    private int _cascadeCount;

    private uint _pointShadowFBO;
    private TexCubeArray? _pointShadowTexture;
    private uint _pointShadowResolution;
    private int _pointShadowCount;
    private ShadowFilterMode _pointShadowFilterMode;
    private int _activePointShadowCount;

    private DirectionalLightComponent? _stagedDirectionalLight;
    private DirectionalLightComponent? _activeDirectionalLight;
    private readonly object _dirLightLock = new();
    
    private readonly float[] _screenQuadVertices =
    [
        //  x, y, z, u, v
        -1.0f,  1.0f, 0.0f, 0.0f, 1.0f,
        -1.0f, -1.0f, 0.0f, 0.0f, 0.0f,
        1.0f,  1.0f, 0.0f, 1.0f, 1.0f,
        1.0f, -1.0f, 0.0f, 1.0f, 0.0f,
    ];
    
    private readonly float[] _skyboxVertices =
    [
        -1.0f,  1.0f, -1.0f,
        -1.0f, -1.0f, -1.0f,
        1.0f, -1.0f, -1.0f,
        1.0f, -1.0f, -1.0f,
        1.0f,  1.0f, -1.0f,
        -1.0f,  1.0f, -1.0f,
 
        -1.0f, -1.0f,  1.0f,
        -1.0f, -1.0f, -1.0f,
        -1.0f,  1.0f, -1.0f,
        -1.0f,  1.0f, -1.0f,
        -1.0f,  1.0f,  1.0f,
        -1.0f, -1.0f,  1.0f,
 
        1.0f, -1.0f, -1.0f,
        1.0f, -1.0f,  1.0f,
        1.0f,  1.0f,  1.0f,
        1.0f,  1.0f,  1.0f,
        1.0f,  1.0f, -1.0f,
        1.0f, -1.0f, -1.0f,
 
        -1.0f, -1.0f,  1.0f,
        -1.0f,  1.0f,  1.0f,
        1.0f,  1.0f,  1.0f,
        1.0f,  1.0f,  1.0f,
        1.0f, -1.0f,  1.0f,
        -1.0f, -1.0f,  1.0f,
 
        -1.0f,  1.0f, -1.0f,
        1.0f,  1.0f, -1.0f,
        1.0f,  1.0f,  1.0f,
        1.0f,  1.0f,  1.0f,
        -1.0f,  1.0f,  1.0f,
        -1.0f,  1.0f, -1.0f,
 
        -1.0f, -1.0f, -1.0f,
        -1.0f, -1.0f,  1.0f,
        1.0f, -1.0f, -1.0f,
        1.0f, -1.0f, -1.0f,
        -1.0f, -1.0f,  1.0f,
        1.0f, -1.0f,  1.0f,
    ];
    
    public ForwardPlusRenderingPipeline(
        in GL gl,
        in GLDebug glDebug,
        in TRenderStage[] renderStages,
        in RenderSettings renderSettings,
        in IWindowContext windowContext,
        in IAssetDatabase<Shader> shaderDatabase,
        in GLContext glContext
    ) : base(renderStages) {
        _gl = gl;
        _glDebug = glDebug;
        _renderSettings = renderSettings;
        _windowContext = windowContext;
        _glContext = glContext;

        Shader depthShader = GetShaderOrDie(shaderDatabase, name: "forward/depth");
        Shader depthCutoutShader = GetShaderOrDie(shaderDatabase, name: "forward/depth_cutout");
        Shader depthCutoutArrayShader = GetShaderOrDie(shaderDatabase, name: "forward/depth_cutout_array");
        Shader computeShader = GetShaderOrDie(shaderDatabase, name:  "forward/cull_lights");
        Shader ssaoShader = GetShaderOrDie(shaderDatabase, name: "forward/ssao");
        Shader skyboxShader = GetShaderOrDie(shaderDatabase, name: "skybox");
        Shader cubemapShader = GetShaderOrDie(shaderDatabase, name: "cubemap");
        Shader blurShader = GetShaderOrDie(shaderDatabase, name: "blur_gaussian");
        Shader bloomShader = GetShaderOrDie(shaderDatabase, name: "bloom");
        
        _depthShader = depthShader.CreateProgram(glContext);
        _depthCutoutShader = depthCutoutShader.CreateProgram(glContext);
        _depthCutoutArrayShader = depthCutoutArrayShader.CreateProgram(glContext);
        _computeShader = computeShader.CreateProgram(glContext);
        _ssaoShader = ssaoShader.CreateProgram(glContext);
        _skyboxShader = skyboxShader.CreateProgram(glContext);
        _cubemapShader = cubemapShader.CreateProgram(glContext);
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
        
        _preDepthTex = new TexImage2D(_gl, name: "prepass_depth", pixels: null, _screenWidth, _screenHeight, TextureFormat.Depth24f, TextureParams.ClampNearest);
        _preDepthFBO = new FramebufferObject(_gl, name: "prepass_depth", _preDepthTex, FramebufferAttachment.DepthAttachment);
        
        _depthTex = new TexImage2D(_gl, name: "depth", pixels: null, _screenWidth, _screenHeight, TextureFormat.Depth24f, TextureParams.ClampNearest);
        _depthFBO = new FramebufferObject(_gl, name: "depth", _depthTex, FramebufferAttachment.DepthAttachment);
        
        _ssaoTex = new TexImage2D(_gl, name: "ssao", pixels: null, _screenWidth, _screenHeight, TextureFormat.R32f, TextureParams.ClampLinear);
        _ssaoFBO = new FramebufferObject(_gl, name: "ssao", _ssaoTex, FramebufferAttachment.ColorAttachment0);
        
        _blurTex = new TexImage2D(_gl, name: "blur", pixels: null, _screenWidth, _screenHeight, TextureFormat.Rgb16f, TextureParams.ClampLinear);
        _blurFBO = new FramebufferObject(_gl, name: "blur", _blurTex, FramebufferAttachment.ColorAttachment0);
        
        _screenTex = new TexImage2D(_gl, name: "screen", pixels: null, _screenWidth, _screenHeight, TextureFormat.Rgba16f, TextureParams.ClampLinear);
        _screenFBO = new FramebufferObject(_gl, name: "screen", _screenTex, FramebufferAttachment.ColorAttachment0);
        
        _screenVBO = new BufferObject<float>(_gl, _screenQuadVertices, BufferTargetARB.ArrayBuffer);
        _screenVAO = new VertexArrayObject<float>(_gl, _screenVBO);
        _screenVAO.SetVertexAttributePointer(index: 0, count: 3, type: VertexAttribPointerType.Float, stride: 5 * sizeof(float), offset: 0);
        _screenVAO.SetVertexAttributePointer(index: 1, count: 2, type: VertexAttribPointerType.Float, stride: 5 * sizeof(float), offset: 3 * sizeof(float));
        
        _skyboxVBO = new BufferObject<float>(_gl, _skyboxVertices, BufferTargetARB.ArrayBuffer);
        _skyboxVAO = new VertexArrayObject<float>(_gl, _skyboxVBO);
        _skyboxVAO.SetVertexAttributePointer(index: 0, count: 3, type: VertexAttribPointerType.Float, stride: 3 * sizeof(float), offset: 0);
        
        _lightsSSBO = new BufferObject<GPULight>(_gl, size: MAX_LIGHTS, BufferTargetARB.ShaderStorageBuffer, BufferUsageARB.DynamicDraw, index: 0);
        _tileIndicesSSBO = new BufferObject<uint>(_gl, size: _numTiles * MAX_LIGHTS_PER_TILE, BufferTargetARB.ShaderStorageBuffer, BufferUsageARB.DynamicDraw, index: 1);
        _tileCountsSSBO = new BufferObject<uint>(_gl, size: _numTiles, BufferTargetARB.ShaderStorageBuffer, BufferUsageARB.DynamicDraw, index: 2);
        
        _colorRBO = new RenderbufferObject(_gl, name: "render_color", _screenWidth, _screenHeight, FramebufferAttachment.ColorAttachment0, InternalFormat.Rgba16f, samples: 4);
        _bloomRBO = new RenderbufferObject(_gl, name: "render_bloom", _screenWidth, _screenHeight, FramebufferAttachment.ColorAttachment1, InternalFormat.Rgba8, samples: 4);
        _depthStencilRBO = new RenderbufferObject(_gl, name: "render_DS", _screenWidth, _screenHeight, FramebufferAttachment.DepthStencilAttachment, InternalFormat.Depth24Stencil8, samples: 4);
        _renderFBO = new FramebufferObject(_gl, name: "render", _screenWidth, _screenHeight, _drawBuffers, renderBuffers: [_colorRBO, _bloomRBO, _depthStencilRBO]);
        
        TextureCubemap? skybox = renderSettings.Skybox.Get();
        if (skybox != null)
        {
            ApplySkybox(skybox);
        }
        
        windowContext.Resized += OnWindowResized;
        renderSettings.Skybox.Changed += OnSkyboxChanged;
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
        DirectionalLightComponent? dirLight = null;
        var queryDirLightAction = new WriteDirectionalLightAction(ref dirLight);
        store.Query<DirectionalLightComponent, WriteDirectionalLightAction>(0f, ref queryDirLightAction);
        lock (_dirLightLock)
        {
            _stagedDirectionalLight = queryDirLightAction.Target;
        }

        lock (_lightsBuffer)
        {
            _lightsBuffer.Clear();
            var bufferLightAction = new WriteLightAction(this);
            store.Query<TransformComponent, LightComponent, WriteLightAction>(0f, ref bufferLightAction);
        }
    }

    private void OnWindowResized(Vector2 size)
    {
        if (size.X <= 0 && size.Y <= 0)
        {
            return;
        }
        
        _screenWidth = (uint)size.X;
        _screenHeight = (uint)size.Y;
        _screenHalfWidth = _screenWidth / 2;
        _screenHalfHeight = _screenHeight / 2;
        
        _numTilesX = (int)(_screenWidth + TILE_WIDTH - 1) / TILE_WIDTH;
        _numTilesY = (int)(_screenHeight + TILE_HEIGHT - 1) / TILE_HEIGHT;
        _numTiles = _numTilesX * _numTilesY;
        _emptyTileCounts = new uint[_numTiles];

        _preDepthFBO.Resize(_screenWidth, _screenHeight);
        _depthFBO.Resize(_screenWidth, _screenHeight);
        _ssaoFBO.Resize(_screenWidth, _screenHeight);
        _blurFBO.Resize(_screenWidth, _screenHeight);
        _screenFBO.Resize(_screenWidth, _screenHeight);
        _glDebug.TryLogError();
        
        _renderFBO.Resize(_screenWidth, _screenHeight);
        _glDebug.TryLogError();
        
        _tileIndicesSSBO.Resize(_numTiles * MAX_LIGHTS_PER_TILE);
        _tileCountsSSBO.Resize(_numTiles);
        _glDebug.TryLogError();
    }

    public override void PreRender(double delta, RenderScene renderScene)
    {
        AntiAliasing antiAliasing = _renderSettings.AntiAliasing.Get();
        _gl.Set(EnableCap.Multisample, antiAliasing == AntiAliasing.MSAA);

        float near = renderScene.Projection.M34 / (renderScene.Projection.M33 - 1.0f);
        float far = renderScene.Projection.M34 / (renderScene.Projection.M33 + 1.0f);
        Matrix4x4.Invert(renderScene.Projection, out Matrix4x4 inverseProjection);

        lock (_dirLightLock)
        {
            _activeDirectionalLight = _stagedDirectionalLight;
        }

        // Collect lights
        GPULight[] lights;
        lock (_lightsBuffer)
        {
            _lightsBuffer.Swap();
            lights = _lightsBuffer.Read();
        }

        // Pre-render stages (populates GLInstancedRenderer instances without transparent meshes)
        PreRenderStages(delta, renderScene, isDepthPass: true);

        // Render dynamic shadows
        RenderShadows(renderScene, near, far, lights);

        if (_renderSettings.SAO.Enabled.Get())
        {
            // Depth pre-pass
            _gl.Viewport(0, 0, _screenHalfWidth, _screenHalfHeight);
            using (_preDepthFBO.Use())
            using (_depthShader.Use())
            {
                _depthShader.SetUniform("view", renderScene.View);
                _depthShader.SetUniform("projection", renderScene.Projection);
                _depthShader.SetUniform("near", near);
                _depthShader.SetUniform("far", far);
            
                _gl.DepthMask(true);
                _gl.Clear((uint)ClearBufferMask.DepthBufferBit);
                _gl.Enable(GLEnum.DepthTest);

                Draw(delta, renderScene, isDepthPass: true);
            }
            _glDebug.TryLogError();
            
            // SSAO pass
            using (_ssaoFBO.Use())
            using (_ssaoShader.Use())
            using (_preDepthTex.Activate(TextureUnit.Texture0))
            {
                _gl.Uniform1(_gl.GetUniformLocation(_ssaoShader.Handle, "uDepthTex"), 0);
                _ssaoShader.SetUniform("uInvProj", inverseProjection);
                _ssaoShader.SetUniform("uRadius", 0.1f);
                _ssaoShader.SetUniform("SIGMA", 0.5f);
                _ssaoShader.SetUniform("SAO_K", 1.5f);
                _ssaoShader.SetUniform("uSampleCount", _renderSettings.SAO.Samples.Get());

                _gl.Clear(ClearBufferMask.ColorBufferBit);

                _screenVAO.Bind();
                _gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
                _screenVAO.Unbind();
            }

            _glDebug.TryLogError();

            // SSAO blur pass
            using (_blurShader.Use())
            using (_ssaoFBO.Use())
            using (_ssaoTex.Activate(TextureUnit.Texture0))
            {
                _blurShader.SetUniform("texture0", 0);

                var horizontal = true;
                for (var i = 0; i < 4; i++)
                {
                    int horizontalInt = horizontal ? 0 : 1;
                    horizontal = !horizontal;

                    _blurShader.SetUniform("uHorizontal", horizontalInt);

                    _screenVAO.Bind();
                    _gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
                    _screenVAO.Unbind();
                }
            }
            _glDebug.TryLogError();
        }

        // Depth full pass
        _gl.Viewport(0, 0, _screenWidth, _screenHeight);
        using (_depthFBO.Use())
        using (_depthShader.Use())
        {
            _depthShader.SetUniform("view", renderScene.View);
            _depthShader.SetUniform("projection", renderScene.Projection);
            _depthShader.SetUniform("near", near);
            _depthShader.SetUniform("far", far);
            
            _gl.DepthMask(true);
            _gl.Clear((uint)ClearBufferMask.DepthBufferBit);
            _gl.Enable(GLEnum.DepthTest);

            Draw(delta, renderScene, isDepthPass: false);
        }
        _glDebug.TryLogError();

        //  Reset to the back buffer
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _gl.Viewport(0, 0, _screenWidth, _screenHeight);
        _glDebug.TryLogError();
        
        //  Update lights
        _lightsSSBO.UpdateData(lights);
        _glDebug.TryLogError();

        // Clear tile counts
        _tileCountsSSBO.UpdateData(_emptyTileCounts);
        _glDebug.TryLogError();

        //  Dispatch tile compute shader
        using (_computeShader.Use())
        using (_depthTex.Activate(TextureUnit.Texture0))
        {
            _computeShader.SetUniform("uScreenSize", (int)_screenWidth, (int)_screenHeight);
            _computeShader.SetUniform("uTileSize", TILE_WIDTH, TILE_HEIGHT);
            _computeShader.SetUniform("uNumLights", lights.Length);
            _computeShader.SetUniform("uMaxLightsPerTile", MAX_LIGHTS_PER_TILE);
            _computeShader.SetUniform("uInvProj", inverseProjection);
            _computeShader.SetUniform("uView", renderScene.View);
            
            var groupsX = (uint)_numTilesX;
            var groupsY = (uint)_numTilesY;
            const int groupsZ = 1;
            _gl.DispatchCompute(groupsX, groupsY, groupsZ);
            _gl.MemoryBarrier(MemoryBarrierMask.ShaderStorageBarrierBit | MemoryBarrierMask.TextureUpdateBarrierBit);
        }
        _glDebug.TryLogError();

        //  Clear and begin drawing to the render buffer
        _renderFBO.Bind();
        _gl.ClearColor(0f, 0f, 0f, 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
        
        //  Skybox pass
        if (_skyboxTex != null)
        {
            _gl.DepthFunc(DepthFunction.Lequal);
            using (_cubemapShader.Use())
            using (_skyboxTex.Activate(TextureUnit.Texture0))
            {
                Matrix4x4 skyboxView = renderScene.View;
                skyboxView.Translation = Vector3.Zero;
                
                _cubemapShader.SetUniform("cubemap", 0);
                _cubemapShader.SetUniform("view", skyboxView);
                _cubemapShader.SetUniform("projection", renderScene.Projection);

                _gl.DrawBuffer(DrawBufferMode.ColorAttachment0);
                _gl.DepthMask(false);
                
                _skyboxVAO.Bind();
                _gl.DrawArrays(PrimitiveType.Triangles, 0, 36);
                _skyboxVAO.Unbind();
                
                _gl.DepthMask(true);
            }
            _gl.DepthFunc(DepthFunction.Less);
            _glDebug.TryLogError();
        }
        else
        {
            using (_skyboxShader.Use())
            {
                Matrix4x4 skyboxView = renderScene.View;
                skyboxView.Translation = Vector3.Zero;
                
                _skyboxShader.SetUniform("uRGB", _renderSettings.AmbientLight.Get());
    
                _gl.DrawBuffer(DrawBufferMode.ColorAttachment0);
                _gl.DepthMask(false);
                
                _screenVAO.Bind();
                _gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
                _screenVAO.Unbind();
                
                _gl.DepthMask(true);
            }
            _glDebug.TryLogError();
        }
        
        //  Reset draw buffers
        _gl.DrawBuffers(_drawBuffers);
        _glDebug.TryLogError();
    }

    public override void PostRender(double delta, RenderScene renderScene)
    {
        _glDebug.TryLogError();

        //  Blit the render buffer to the back buffer to display it
        _renderFBO.Blit(ReadBufferMode.ColorAttachment0, DrawBufferMode.Back, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);
        _glDebug.TryLogError();

        if (_renderSettings.Bloom.Enabled.Get())
        {
            //  Bloom pre-pass
            //  Blit the bloom renderbuffer to the blur texture
            _renderFBO.Blit(_blurFBO, ReadBufferMode.ColorAttachment1, DrawBufferMode.ColorAttachment0, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);
            _glDebug.TryLogError();

            //  Blur the bloom texture
            //  TODO Blur should be done with separate textures for
            //       the horizontal and vertical blurs. This creates artifacts,
            //       but they aren't too noticeable. since this is just for bloom.
            //       Using a single texture and single pass is a bit more performant.
            //       Whether one or two textures is used could be driven by quality settings.
            using (_blurShader.Use())
            using (_blurFBO.Use())
            using (_blurTex.Activate(TextureUnit.Texture0))
            {
                _blurShader.SetUniform("texture0", 0);

                var horizontal = true;
                for (var i = 0; i <= _renderSettings.Bloom.Passes.Get(); i++)
                {
                    int horizontalInt = horizontal ? 0 : 1;
                    horizontal = !horizontal;

                    _blurShader.SetUniform("uHorizontal", horizontalInt);

                    _screenVAO.Bind();
                    _gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
                    _screenVAO.Unbind();
                }
            }

            _glDebug.TryLogError();

            //  Blit the bloom renderbuffer to the screen texture
            _renderFBO.Blit(_screenFBO, ReadBufferMode.ColorAttachment1, DrawBufferMode.ColorAttachment0, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);
            _glDebug.TryLogError();
            
            //  Return to the back buffer
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            _gl.Viewport(0, 0, _screenWidth, _screenHeight);
            
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

            _glDebug.TryLogError();
        }
    }

    private void RenderShadows(RenderScene renderScene, float near, float far, GPULight[] lights)
    {
        if (!_renderSettings.Shadows.Enabled.Get())
        {
            _cascadeCount = 0;
            _activePointShadowCount = 0;
            for (var i = 0; i < lights.Length; i++)
            {
                lights[i].ShadowParams = new Vector4(-1f, 0f, 0f, 0f);
            }
            return;
        }

        // Directional Shadows (Cascaded Shadow Maps)
        if (_activeDirectionalLight.HasValue && _activeDirectionalLight.Value.CastShadows)
        {
            uint resolution = Math.Max(256u, (uint)_renderSettings.Shadows.Resolution.Get());
            int cascadeCount = Math.Clamp(_renderSettings.Shadows.CascadeCount.Get(), 1, 4);
            ShadowFilterMode filterMode = _renderSettings.Shadows.FilterMode.Get();
            EnsureCSM(resolution, cascadeCount, filterMode);

            _cascadeCount = cascadeCount;
            Span<float> splits = stackalloc float[4];
            ShadowCascadeMath.CalculateCascadeSplits(near, far, _renderSettings.Shadows.MaxDistance.Get(), cascadeCount, splits);
            _cascadeSplits = new Vector4(splits[0], splits[1], splits[2], splits[3]);

            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _csmFBO);
            _gl.DrawBuffer(DrawBufferMode.None);
            _gl.ReadBuffer(ReadBufferMode.None);
            _gl.Enable(EnableCap.DepthTest);
            _gl.DepthFunc(DepthFunction.Less);
            _gl.Enable(EnableCap.CullFace);
            _gl.CullFace(TriangleFace.Back);

            float fov = _renderSettings.FOV.Get() * ((float)Math.PI / 180f);
            float aspect = (float)_screenWidth / Math.Max(1u, _screenHeight);
            Vector3 lightDir = _activeDirectionalLight.Value.Direction;

            float lastSplit = near;
            for (var i = 0; i < cascadeCount; i++)
            {
                float currentSplit = splits[i];
                ShadowCascadeMath.CalculateCascadeMatrix(
                    renderScene.View,
                    fov,
                    aspect,
                    lastSplit,
                    currentSplit,
                    lightDir,
                    resolution,
                    out Matrix4x4 cascadeView,
                    out Matrix4x4 cascadeProj
                );
                _cascadeViewProj[i] = cascadeView * cascadeProj;
                lastSplit = currentSplit;

                _gl.FramebufferTextureLayer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, _csmTexture!.Handle, 0, i);
                _gl.Viewport(0, 0, resolution, resolution);
                _gl.Clear(ClearBufferMask.DepthBufferBit);

                DrawShadowPass(cascadeView, cascadeProj, _depthShader, _depthCutoutShader, _depthCutoutArrayShader);
            }
        }
        else
        {
            _cascadeCount = 0;
        }

        // Point Shadows
        int maxPointShadows = Math.Clamp(_renderSettings.Shadows.MaxPointShadows.Get(), 0, 8);
        if (maxPointShadows > 0 && lights.Length > 0)
        {
            Span<int> candidateIndices = stackalloc int[Math.Min(64, lights.Length)];
            Span<float> candidateScores = stackalloc float[Math.Min(64, lights.Length)];
            int candidateCount = 0;

            Vector3 camPos = renderScene.View.GetCameraPosition();
            Vector3 camForward = renderScene.View.GetCameraForward();
            float maxShadowDist = _renderSettings.Shadows.MaxDistance.Get();

            for (var i = 0; i < lights.Length; i++)
            {
                if (lights[i].ShadowParams.X > 0.0f)
                {
                    Vector3 lightPos = new(lights[i].PosRadius.X, lights[i].PosRadius.Y, lights[i].PosRadius.Z);
                    float lightRadius = lights[i].PosRadius.W;
                    float distSq = Vector3.DistanceSquared(lightPos, camPos);

                    float maxReach = maxShadowDist + lightRadius;
                    if (distSq <= maxReach * maxReach)
                    {
                        Vector3 toLight = lightPos - camPos;
                        float forwardDot = Vector3.Dot(toLight, camForward);

                        float score = distSq;
                        if (forwardDot < -lightRadius)
                        {
                            score += 100000f;
                        }

                        if (candidateCount < candidateIndices.Length)
                        {
                            candidateIndices[candidateCount] = i;
                            candidateScores[candidateCount] = score;
                            candidateCount++;
                        }
                        else
                        {
                            int worstIdx = 0;
                            float worstScore = candidateScores[0];
                            for (var c = 1; c < candidateCount; c++)
                            {
                                if (candidateScores[c] > worstScore)
                                {
                                    worstScore = candidateScores[c];
                                    worstIdx = c;
                                }
                            }

                            if (score < worstScore)
                            {
                                candidateIndices[worstIdx] = i;
                                candidateScores[worstIdx] = score;
                            }
                        }
                    }
                }
                
                lights[i].ShadowParams = new Vector4(-1f, 0f, 0f, 0f);
            }

            // Sort candidate indices by score ascending
            for (var i = 0; i < candidateCount - 1; i++)
            {
                for (var j = i + 1; j < candidateCount; j++)
                {
                    if (!(candidateScores[j] < candidateScores[i]))
                    {
                        continue;
                    }
                    
                    (candidateScores[i], candidateScores[j]) = (candidateScores[j], candidateScores[i]);
                    (candidateIndices[i], candidateIndices[j]) = (candidateIndices[j], candidateIndices[i]);
                }
            }

            int m = Math.Min(candidateCount, maxPointShadows);
            _activePointShadowCount = m;

            if (m > 0)
            {
                uint pointRes = Math.Max(128u, Math.Min(1024u, (uint)_renderSettings.Shadows.Resolution.Get() / 2));
                ShadowFilterMode filterMode = _renderSettings.Shadows.FilterMode.Get();
                EnsurePointShadows(pointRes, maxPointShadows, filterMode);

                _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _pointShadowFBO);
                _gl.DrawBuffer(DrawBufferMode.None);
                _gl.ReadBuffer(ReadBufferMode.None);
                _gl.Enable(EnableCap.DepthTest);
                _gl.DepthFunc(DepthFunction.Less);
                _gl.Enable(EnableCap.CullFace);
                _gl.CullFace(TriangleFace.Back);

                Span<Matrix4x4> faceViews = stackalloc Matrix4x4[6];

                for (var k = 0; k < m; k++)
                {
                    int lightIdx = candidateIndices[k];
                    Vector3 lightPos = new(lights[lightIdx].PosRadius.X, lights[lightIdx].PosRadius.Y, lights[lightIdx].PosRadius.Z);
                    float lightRadius = lights[lightIdx].PosRadius.W;

                    Matrix4x4 pointProj = ShadowCascadeMath.CreatePerspectiveOpenGL((float)Math.PI * 0.5f, 1.0f, 0.1f, lightRadius);

                    faceViews[0] = Matrix4x4.CreateLookAt(lightPos, lightPos + Vector3.UnitX, -Vector3.UnitY);
                    faceViews[1] = Matrix4x4.CreateLookAt(lightPos, lightPos - Vector3.UnitX, -Vector3.UnitY);
                    faceViews[2] = Matrix4x4.CreateLookAt(lightPos, lightPos + Vector3.UnitY, Vector3.UnitZ);
                    faceViews[3] = Matrix4x4.CreateLookAt(lightPos, lightPos - Vector3.UnitY, -Vector3.UnitZ);
                    faceViews[4] = Matrix4x4.CreateLookAt(lightPos, lightPos + Vector3.UnitZ, -Vector3.UnitY);
                    faceViews[5] = Matrix4x4.CreateLookAt(lightPos, lightPos - Vector3.UnitZ, -Vector3.UnitY);

                    for (var f = 0; f < 6; f++)
                    {
                        _gl.FramebufferTextureLayer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, _pointShadowTexture!.Handle, 0, (k * 6) + f);
                        _gl.Viewport(0, 0, pointRes, pointRes);
                        _gl.Clear(ClearBufferMask.DepthBufferBit);

                        DrawShadowPass(faceViews[f], pointProj, _depthShader, _depthCutoutShader, _depthCutoutArrayShader);
                    }

                    lights[lightIdx].ShadowParams = new Vector4(k, _renderSettings.Shadows.DepthBias.Get(), 0f, 0f);
                }
            }
        }
        else
        {
            _activePointShadowCount = 0;
            for (var i = 0; i < lights.Length; i++)
            {
                lights[i].ShadowParams = new Vector4(-1f, 0f, 0f, 0f);
            }
        }

        // Restore backbuffer and screen viewport
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _gl.Viewport(0, 0, _screenWidth, _screenHeight);
    }

    private void EnsureCSM(uint resolution, int cascadeCount, ShadowFilterMode filterMode)
    {
        if (_csmFBO == 0)
        {
            _csmFBO = _gl.GenFramebuffer();
        }

        if (_csmTexture != null &&
            _csmResolution == resolution &&
            _csmCascadeCount == cascadeCount &&
            _csmFilterMode == filterMode
        ) {
            return;
        }
        
        _csmTexture?.Dispose();
        _csmResolution = resolution;
        _csmCascadeCount = cascadeCount;
        _csmFilterMode = filterMode;

        TextureParams @params = filterMode == ShadowFilterMode.Soft ? TextureParams.ShadowMap : TextureParams.ShadowMapNearest;
        _csmTexture = new TexImage3D(_gl, "csm_depth", pixels: null, resolution, resolution, (uint)cascadeCount, TextureFormat.Depth24f, @params);
    }

    private void EnsurePointShadows(uint resolution, int lightCount, ShadowFilterMode filterMode)
    {
        if (_pointShadowFBO == 0)
        {
            _pointShadowFBO = _gl.GenFramebuffer();
        }

        if (_pointShadowTexture != null &&
            _pointShadowResolution == resolution &&
            _pointShadowCount == lightCount &&
            _pointShadowFilterMode == filterMode
        ) {
            return;
        }
        
        _pointShadowTexture?.Dispose();
        _pointShadowResolution = resolution;
        _pointShadowCount = lightCount;
        _pointShadowFilterMode = filterMode;

        TextureParams @params = filterMode == ShadowFilterMode.Soft ? TextureParams.ShadowMap : TextureParams.ShadowMapNearest;
        _pointShadowTexture = new TexCubeArray(_gl, "point_shadow_depth", resolution, resolution, (uint)lightCount, TextureFormat.Depth16f, @params);
    }

    protected override void ShaderActivationCallback(ShaderProgram shader)
    {
        shader.SetUniform("uScreenSize", (int)_screenWidth, (int)_screenHeight);
        shader.SetUniform("uTileSize", TILE_WIDTH, TILE_HEIGHT);
        shader.SetUniform("uMaxLightsPerTile", MAX_LIGHTS_PER_TILE);
        shader.SetUniform("uAmbientLight", _renderSettings.AmbientLight.Get());

        shader.SetUniform("uEnableAO", _renderSettings.SAO.Enabled.Get() ? 1f : 0f);

        _ssaoTex.Activate(TextureUnit.Texture5);
        shader.SetUniform("uAO", 5);

        _preDepthTex.Activate(TextureUnit.Texture6);
        shader.SetUniform("uPreDepth", 6);
        
        _depthTex.Activate(TextureUnit.Texture7);
        shader.SetUniform("uDepth", 7);

        bool shadowsEnabled = _renderSettings.Shadows.Enabled.Get();
        shader.SetUniform("uShadowsEnabled", shadowsEnabled ? 1 : 0);
        shader.SetUniform("uShadowFilterMode", (int)_renderSettings.Shadows.FilterMode.Get());
        shader.SetUniform("uNormalBias", _renderSettings.Shadows.NormalBias.Get());
        shader.SetUniform("uDepthBias", _renderSettings.Shadows.DepthBias.Get());

        if (_activeDirectionalLight.HasValue)
        {
            DirectionalLightComponent dirLight = _activeDirectionalLight.Value;
            shader.SetUniform("uDirectionalLightDir", dirLight.Direction);
            shader.SetUniform("uDirectionalLightColor", dirLight.Color);
            shader.SetUniform("uDirectionalLightIntensity", dirLight.Intensity);
            shader.SetUniform("uDirectionalLightCastShadows", dirLight.CastShadows ? 1 : 0);
        }
        else
        {
            shader.SetUniform("uDirectionalLightIntensity", 0.0f);
            shader.SetUniform("uDirectionalLightCastShadows", 0);
        }

        if (shadowsEnabled && _csmTexture != null && _cascadeCount > 0)
        {
            _csmTexture.Activate(TextureUnit.Texture8);
            shader.SetUniform("uDirectionalShadowMap", 8);
            shader.SetUniform("uCascadeCount", _cascadeCount);
            shader.SetUniform("uCascadeSplits", _cascadeSplits);
            shader.SetUniform("uCascadeViewProj", new ReadOnlySpan<Matrix4x4>(_cascadeViewProj, 0, _cascadeCount));
        }
        else
        {
            shader.SetUniform("uCascadeCount", 0);
        }

        if (shadowsEnabled && _pointShadowTexture != null && _activePointShadowCount > 0)
        {
            _pointShadowTexture.Activate(TextureUnit.Texture9);
            shader.SetUniform("uPointShadowMap", 9);
        }
    }
    
    public void Dispose()
    {
        _windowContext.Resized -= OnWindowResized;
        _renderSettings.Skybox.Changed -= OnSkyboxChanged;

        _csmTexture?.Dispose();
        _pointShadowTexture?.Dispose();

        if (_csmFBO != 0)
        {
            _gl.DeleteFramebuffer(_csmFBO);
            _csmFBO = 0;
        }

        if (_pointShadowFBO != 0)
        {
            _gl.DeleteFramebuffer(_pointShadowFBO);
            _pointShadowFBO = 0;
        }
    }

    private void OnSkyboxChanged(object? sender, DataChangedEventArgs<TextureCubemap?> e)
    {
        TexCubemap? currentSkyboxTex = _skyboxTex;
        currentSkyboxTex?.Dispose();
        if (e.NewValue == null)
        {
            _skyboxTex = null;
            return;
        }
        
        ApplySkybox(e.NewValue);
    }
    
    private void ApplySkybox(TextureCubemap skybox)
    {
        var pixels = new byte[6][];
        for (var i = 0; i < skybox.Textures.Length; i++)
        {
            pixels[i] = skybox.Textures[i].Pixels;
        }
        _skyboxTex = _glContext.CreateTexCubemap(skybox.Name, pixels, (uint)skybox.Width, (uint)skybox.Height, TextureFormat.Rgba, TextureParams.ClampNearest);
    }
    
    private struct WriteDirectionalLightAction(ref DirectionalLightComponent? target) : IForEach<DirectionalLightComponent>
    {
        public DirectionalLightComponent? Target = target;

        public void Execute(float delta, DataStore store, int entity, in DirectionalLightComponent light)
        {
            Target = light;
        }
    }

    private readonly struct WriteLightAction(in ForwardPlusRenderingPipeline<TRenderStage> owner) : IForEach<TransformComponent, LightComponent>
    {
        private readonly ForwardPlusRenderingPipeline<TRenderStage> _owner = owner;
        
        public void Execute(float delta, DataStore store, int entity, in TransformComponent transform, in LightComponent light)
        {
            var posRadius = new Vector4(transform.Position.X, transform.Position.Y, transform.Position.Z, light.Radius);
            var colorIntensity = new Vector4(light.Color.X, light.Color.Y, light.Color.Z, light.Size);
            var shadowParams = new Vector4(light.CastShadows ? 1.0f : -1.0f, 0f, 0f, 0f);
            _owner._lightsBuffer.Write(new GPULight(posRadius, colorIntensity, shadowParams));
        }
    }
}