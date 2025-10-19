using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.OpenGL;
using Swordfish.Graphics.SilkNET.OpenGL.Util;
using Swordfish.Library.Collections;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Extensions;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using Swordfish.Settings;

namespace Swordfish.Graphics.SilkNET.OpenGL.Pipelines;

internal sealed unsafe class ForwardPlusRenderingPipeline<TRenderStage> : RenderPipeline<TRenderStage>
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
    
    private readonly uint _depthTex;
    private readonly uint _depthFBO;
    
    private readonly uint _ssaoTex;
    private readonly uint _ssaoFBO;
    
    private readonly uint _renderFBO;
    private readonly uint _colorRBO;
    private readonly uint _bloomRBO;
    private readonly uint _depthStencilRBO;

    private readonly uint _blurTex;
    private readonly uint _blurFBO;
    
    private readonly uint _screenTex;
    private readonly uint _screenFBO;
    
    private readonly uint _lightsSSBO;
    private readonly uint _tileIndicesSSBO;
    private readonly uint _tileCountsSSBO;
    
    private readonly VertexArrayObject<float> _screenVAO;

    private readonly DrawBufferMode[] _drawBuffers = [DrawBufferMode.ColorAttachment0, DrawBufferMode.ColorAttachment1];
    private readonly List<LightData> _lights = [];
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
        in GLContext glContext,
        in IShortcutService shortcutService
    ) : base(renderStages) {
        _gl = gl;
        _renderSettings = renderSettings;
        
        const string depthShaderName = "forwardplus_depth";
        Result<Shader> depthShader = shaderDatabase.Get(depthShaderName);
        if (!depthShader)
        {
            throw new FatalAlertException($"Failed to load the forward+ renderer's shader \"{depthShaderName}\".");
        }
        
        const string fragmentShaderName = "forwardplus_fragment";
        Result<Shader> fragmentShader = shaderDatabase.Get(fragmentShaderName);
        if (!fragmentShader)
        {
            throw new FatalAlertException($"Failed to load the forward+ renderer's shader \"{fragmentShaderName}\".");
        }
        
        const string computeShaderName = "fowardplus_compute";
        Result<Shader> computeShader = shaderDatabase.Get(computeShaderName);
        if (!computeShader)
        {
            throw new FatalAlertException($"Failed to load the forward+ renderer's shader \"{computeShaderName}\".");
        }
        
        const string ssaoShaderName = "fowardplus_ssao";
        Result<Shader> ssaoShader = shaderDatabase.Get(ssaoShaderName);
        if (!ssaoShader)
        {
            throw new FatalAlertException($"Failed to load the forward+ renderer's shader \"{ssaoShaderName}\".");
        }
        
        const string skyboxShaderName = "skybox";
        Result<Shader> skyboxShader = shaderDatabase.Get(skyboxShaderName);
        if (!skyboxShader)
        {
            throw new FatalAlertException($"Failed to load the forward+ renderer's shader \"{skyboxShaderName}\".");
        }
        
        const string blurShaderName = "blur_gaussian";
        Result<Shader> blurShader = shaderDatabase.Get(blurShaderName);
        if (!blurShader)
        {
            throw new FatalAlertException($"Failed to load the forward+ renderer's shader \"{blurShaderName}\".");
        }
        
        const string bloomShaderName = "bloom";
        Result<Shader> bloomShader = shaderDatabase.Get(bloomShaderName);
        if (!bloomShader)
        {
            throw new FatalAlertException($"Failed to load the forward+ renderer's shader \"{bloomShaderName}\".");
        }
        
        _depthShader = depthShader.Value.CreateProgram(glContext);
        _computeShader = computeShader.Value.CreateProgram(glContext);
        _ssaoShader = ssaoShader.Value.CreateProgram(glContext);
        _skyboxShader = skyboxShader.Value.CreateProgram(glContext);
        _blurShader = blurShader.Value.CreateProgram(glContext);
        _bloomShader = bloomShader.Value.CreateProgram(glContext);
        
        _screenWidth = (uint)windowContext.Resolution.X;
        _screenHeight = (uint)windowContext.Resolution.Y;
        _screenHalfWidth = _screenWidth / 2;
        _screenHalfHeight = _screenHeight / 2;
        
        _numTilesX = (int)(_screenWidth + TILE_WIDTH - 1) / TILE_WIDTH;
        _numTilesY = (int)(_screenHeight + TILE_HEIGHT - 1) / TILE_HEIGHT;
        _numTiles = _numTilesX * _numTilesY;
        
        _depthTex = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, _depthTex);
        gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.DepthComponent24, _screenWidth, _screenHeight, 0, GLEnum.DepthComponent, GLEnum.Float, null);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        
        _depthFBO = gl.GenFramebuffer();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _depthFBO);
        gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, _depthTex, 0);
        GLEnum status = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        if (status != GLEnum.FramebufferComplete)
        {
            throw new FatalAlertException("Forward+ renderer framebuffer is incomplete.");
        }
        
        _ssaoTex = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, _ssaoTex);
        gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.R32f, _screenHalfWidth, _screenHalfHeight, 0, PixelFormat.Red, PixelType.Float, null);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        
        _ssaoFBO = gl.GenFramebuffer();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _ssaoFBO);
        gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _ssaoTex, 0);
        status = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        if (status != GLEnum.FramebufferComplete)
        {
            throw new FatalAlertException("Forward+ SSAO framebuffer is incomplete.");
        }
        
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
        status = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        gl.BindRenderbuffer(GLEnum.Renderbuffer, 0);
        if (status != GLEnum.FramebufferComplete)
        {
            throw new FatalAlertException("Forward+ render framebuffer is incomplete.");
        }

        _blurFBO = gl.GenFramebuffer();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _blurFBO);
        _blurTex = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, _blurTex);
        gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgb16f, _screenWidth, _screenHeight, 0, PixelFormat.Rgb, PixelType.Float, null);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _blurTex, 0);
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        
        _screenTex = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, _screenTex);
        gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba16f, _screenWidth, _screenHeight, 0, PixelFormat.Rgba, PixelType.Float, null);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        
        _screenFBO = gl.GenFramebuffer();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _screenFBO);
        gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _screenTex, 0);
        status = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
        {
            throw new FatalAlertException("Forward+ screen framebuffer is incomplete.");
        }
        
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        gl.BindTexture(TextureTarget.Texture2D, 0);
        
        var quadVBO = new BufferObject<float>(_gl, _quadVertices, BufferTargetARB.ArrayBuffer);
        _screenVAO = new VertexArrayObject<float>(_gl, quadVBO);
        _screenVAO.SetVertexAttributePointer(0, 3, VertexAttribPointerType.Float, 5 * sizeof(float), 0);
        _screenVAO.SetVertexAttributePointer(1, 2, VertexAttribPointerType.Float, 5 * sizeof(float), 3 * sizeof(float));

        _lightsSSBO = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, _lightsSSBO);
        int lightsByteSize = MAX_LIGHTS * Marshal.SizeOf<GPULight>();
        gl.BufferData(BufferTargetARB.ShaderStorageBuffer, (nuint)lightsByteSize, null, BufferUsageARB.DynamicDraw);
        gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 0, _lightsSSBO);
        
        _tileIndicesSSBO = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, _tileIndicesSSBO);
        int indicesCount = _numTiles * MAX_LIGHTS_PER_TILE;
        gl.BufferData(BufferTargetARB.ShaderStorageBuffer, (nuint)(indicesCount * sizeof(uint)), null, BufferUsageARB.DynamicDraw);
        gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 1, _tileIndicesSSBO);
        
        _tileCountsSSBO = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, _tileCountsSSBO);
        gl.BufferData(BufferTargetARB.ShaderStorageBuffer, (nuint)(_numTiles * sizeof(uint)), null, BufferUsageARB.DynamicDraw);
        gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 2, _tileCountsSSBO);
        
        gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, 0);
        
        windowContext.Resized += OnWindowResized;
        
        _lights.Add(new LightData
        {
            Position = new Vector3(-5f, 10f, 1f),
            Radius = 20f,
            Color = new Vector3(1f, 1f, 1f),
            Intensity = 5f,
        });
        
        _lights.Add(new LightData
        {
            Position = new Vector3(10f, 5f, 1f),
            Radius = 20f,
            Color = new Vector3(1f, 1f, 1f),
            Intensity = 5f,
        });
        
        Shortcut lightShortcut = new(
            "Add lights",
            "General",
            ShortcutModifiers.None,
            Key.F1,
            Shortcut.DefaultEnabled,
            () =>
            {
                lock (_lights)
                {
                    for (int i = 0; i < 50; i++)
                    {
                        _lights.Add(new LightData
                        {
                            Position = new Vector3(Random.Shared.NextSingle() * 30f - 15f,
                                Random.Shared.NextSingle() * 10f - 5f, Random.Shared.NextSingle() * 30f - 15f),
                            Radius = Random.Shared.NextSingle() * 5f + 2f,
                            Color = new Vector3(Random.Shared.NextSingle() + 0.1f, Random.Shared.NextSingle() + 0.1f,
                                Random.Shared.NextSingle() + 0.1f),
                            Intensity = Random.Shared.NextSingle() * 5f + 2f,
                        });
                    }
                    Console.WriteLine($"Lights: {_lights.Count}");
                }
            }
        );
        shortcutService.RegisterShortcut(lightShortcut);
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
        
        // Depth pre-pass
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _depthFBO);
        _gl.Viewport(0, 0, _screenWidth, _screenHeight);
        _gl.DepthMask(true);
        _gl.Clear((uint)ClearBufferMask.DepthBufferBit);
        _gl.Enable(GLEnum.DepthTest);
        
        float near = projection.M34 / (projection.M33 - 1.0f);
        float far  = projection.M34 / (projection.M33 + 1.0f);
        _depthShader.Activate();
        _depthShader.SetUniform("view", view);
        _depthShader.SetUniform("projection", projection);
        _depthShader.SetUniform("near", near);
        _depthShader.SetUniform("far", far);
        
        Draw(delta, view, projection);
        
        // SSAO pass
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _ssaoFBO);
        _gl.Viewport(0, 0, _screenHalfWidth, _screenHalfHeight);
        _gl.Clear(ClearBufferMask.ColorBufferBit);

        _ssaoShader.Activate();
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _depthTex);
        _gl.Uniform1(_gl.GetUniformLocation(_ssaoShader.Handle, "uDepthTex"), 0);
        Matrix4x4.Invert(projection, out Matrix4x4 inverseProjection);
        _ssaoShader.SetUniform("uInvProj", inverseProjection);
        
        _screenVAO.Bind();
        _gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
        _screenVAO.Unbind();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _gl.Viewport(0, 0, _screenWidth, _screenHeight);

        // Upload lights
        int numLights;
        GPULight[] gpuLights;
        lock (_lights)
        {
            numLights = _lights.Count;
            gpuLights = new GPULight[numLights];
            for (var i = 0; i < numLights; ++i)
            {
                LightData light = _lights[i];
                var worldPos = new Vector4(light.Position.X, light.Position.Y, light.Position.Z, 1.0f);
                gpuLights[i].PosRadius = new Vector4(worldPos.X, worldPos.Y, worldPos.Z, light.Radius);
                gpuLights[i].ColorIntensity = new Vector4(light.Color.X, light.Color.Y, light.Color.Z, light.Intensity);
            }
        }
                
        _gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, _lightsSSBO);
        int bytes = numLights * Marshal.SizeOf<GPULight>();
        fixed (GPULight* p = gpuLights)
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
        _gl.Uniform1(_gl.GetUniformLocation(_computeShader.Handle, "uNumLights"), numLights);
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
        shader.SetUniform("uAO", 5);
        shader.SetUniform("uAmbientLight", _ambientLight);
        
        _gl.ActiveTexture(TextureUnit.Texture5);
        _gl.BindTexture(TextureTarget.Texture2D, _ssaoTex);
    }
}