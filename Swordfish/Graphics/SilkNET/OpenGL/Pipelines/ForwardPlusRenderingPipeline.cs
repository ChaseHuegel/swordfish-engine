using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.OpenGL;
using Swordfish.Graphics.SilkNET.OpenGL.Util;
using Swordfish.Library.Collections;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Util;
using Swordfish.Settings;

namespace Swordfish.Graphics.SilkNET.OpenGL.Pipelines;

internal sealed unsafe class ForwardPlusRenderingPipeline<TRenderStage> : RenderPipeline<TRenderStage>
    where TRenderStage : IRenderStage
{
    [StructLayout(LayoutKind.Sequential)]
    private struct GPULight {
        public Vector4 PosRadius;       // x,y,z,r
        public Vector4 ColorIntensity;  // r,g,b,intensity
    }
    
    private const int TILE_WIDTH = 16;
    private const int TILE_HEIGHT = 16;
    private const int MAX_LIGHTS = 1024;
    private const int MAX_LIGHTS_PER_TILE = 256; 
    
    private readonly GL _gl;
    private readonly RenderSettings _renderSettings;
    private readonly IWindowContext _windowContext;
    
    private readonly ShaderProgram _depthShader;
    private readonly ShaderProgram _fragmentShader;
    private readonly ShaderProgram _computeShader;
    
    private readonly int _numTilesX;
    private readonly int _numTilesY;
    private readonly int _numTiles;
    
    private readonly uint _depthTex;
    private readonly uint _depthFBO;
    private readonly uint _lightSSBO;
    private readonly uint _tileIndicesSSBO;
    private readonly uint _tileCountsSSBO;
    
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
        _windowContext = windowContext;
        
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
        
        _depthShader = depthShader.Value.CreateProgram(glContext);
        _fragmentShader = fragmentShader.Value.CreateProgram(glContext);
        _computeShader = computeShader.Value.CreateProgram(glContext);

        var screenWidth = (int)_windowContext.Resolution.X;
        var screenHeight = (int)_windowContext.Resolution.Y;

        _numTilesX = (screenWidth + TILE_WIDTH - 1) / TILE_WIDTH;
        _numTilesY = (screenHeight + TILE_HEIGHT - 1) / TILE_HEIGHT;
        _numTiles = _numTilesX * _numTilesY;
        
        _depthTex = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, _depthTex);
        gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.DepthComponent24, (uint)screenWidth, (uint)screenHeight, 0, GLEnum.DepthComponent, GLEnum.Float, null);
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

        _lightSSBO = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, _lightSSBO);
        int lightsByteSize = MAX_LIGHTS * Marshal.SizeOf<GPULight>();
        gl.BufferData(BufferTargetARB.ShaderStorageBuffer, (nuint)lightsByteSize, null, BufferUsageARB.DynamicDraw);
        gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 0, _lightSSBO);

        _tileIndicesSSBO = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, _tileIndicesSSBO);
        // flattened indices buffer
        int indicesCount = _numTiles * MAX_LIGHTS_PER_TILE;
        gl.BufferData(BufferTargetARB.ShaderStorageBuffer, (nuint)(indicesCount * sizeof(uint)), null, BufferUsageARB.DynamicDraw);
        gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 1, _tileIndicesSSBO);

        _tileCountsSSBO = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, _tileCountsSSBO);
        gl.BufferData(BufferTargetARB.ShaderStorageBuffer, (nuint)(_numTiles * sizeof(uint)), null, BufferUsageARB.DynamicDraw);
        gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 2, _tileCountsSSBO);
        
        gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, 0);
    }
    
    public override void PreRender(double delta, Matrix4x4 view, Matrix4x4 projection)
    {
        AntiAliasing antiAliasing = _renderSettings.AntiAliasing.Get();
        _gl.Set(EnableCap.Multisample, antiAliasing == AntiAliasing.MSAA);

        _gl.ClearColor(0f, 0f, 0f, 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
    }

    public override void PostRender(double delta, Matrix4x4 view, Matrix4x4 projection)
    {
    }
}