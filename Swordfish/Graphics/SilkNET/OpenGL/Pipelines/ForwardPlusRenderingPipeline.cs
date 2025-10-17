using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.OpenGL;
using Swordfish.Graphics.SilkNET.OpenGL.Util;
using Swordfish.Library.Collections;
using Swordfish.Library.Diagnostics;
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
    private const int MAX_LIGHTS_PER_TILE = 256;
    
    private readonly GL _gl;
    private readonly RenderSettings _renderSettings;
    private readonly IWindowContext _windowContext;
    
    private readonly ShaderProgram _depthShader;
    private readonly ShaderProgram _fragmentShader;
    private readonly ShaderProgram _computeShader;
    private readonly ShaderProgram _ssaoShader;
    
    private readonly int _screenWidth;
    private readonly int _screenHeight;
    
    private readonly int _numTilesX;
    private readonly int _numTilesY;
    private readonly int _numTiles;
    
    private readonly uint _depthTex;
    private readonly uint _depthFBO;
    
    private readonly uint _ssaoTex;
    private readonly uint _ssaoFBO;
    
    private readonly uint _lightsSSBO;
    private readonly uint _tileIndicesSSBO;
    private readonly uint _tileCountsSSBO;
    
    private readonly VertexArrayObject<float> _screenVAO;
    
    private readonly float[] _quadVertices =
    [
        //  x, y, z, u, v
        -1.0f,  1.0f, 0.0f, 0.0f, 1.0f,
        -1.0f, -1.0f, 0.0f, 0.0f, 0.0f,
        1.0f,  1.0f, 0.0f, 1.0f, 1.0f,
        1.0f, -1.0f, 0.0f, 1.0f, 0.0f,
    ];

    private readonly List<LightData> _lights = [];
    
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
        
        const string ssaoShaderName = "fowardplus_ssao";
        Result<Shader> ssaoShader = shaderDatabase.Get(ssaoShaderName);
        if (!ssaoShader)
        {
            throw new FatalAlertException($"Failed to load the forward+ renderer's shader \"{ssaoShaderName}\".");
        }
        
        _depthShader = depthShader.Value.CreateProgram(glContext);
        _fragmentShader = fragmentShader.Value.CreateProgram(glContext);
        _computeShader = computeShader.Value.CreateProgram(glContext);
        _ssaoShader = ssaoShader.Value.CreateProgram(glContext);
        
        _screenWidth = (int)_windowContext.Resolution.X;
        _screenHeight = (int)_windowContext.Resolution.Y;
        
        _numTilesX = (_screenWidth + TILE_WIDTH - 1) / TILE_WIDTH;
        _numTilesY = (_screenHeight + TILE_HEIGHT - 1) / TILE_HEIGHT;
        _numTiles = _numTilesX * _numTilesY;
        
        _depthTex = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, _depthTex);
        gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.DepthComponent24, (uint)_screenWidth, (uint)_screenHeight, 0, GLEnum.DepthComponent, GLEnum.Float, null);
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
        gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.R32f, (uint)_screenWidth, (uint)_screenHeight, 0, PixelFormat.Red, PixelType.Float, null);
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
        // flattened indices buffer
        int indicesCount = _numTiles * MAX_LIGHTS_PER_TILE;
        gl.BufferData(BufferTargetARB.ShaderStorageBuffer, (nuint)(indicesCount * sizeof(uint)), null, BufferUsageARB.DynamicDraw);
        gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 1, _tileIndicesSSBO);
        
        _tileCountsSSBO = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, _tileCountsSSBO);
        gl.BufferData(BufferTargetARB.ShaderStorageBuffer, (nuint)(_numTiles * sizeof(uint)), null, BufferUsageARB.DynamicDraw);
        gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 2, _tileCountsSSBO);
        
        gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, 0);
        
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
                            Position = new Vector3(Random.Shared.NextSingle() * 20f - 10f,
                                Random.Shared.NextSingle() * 20f - 10f, Random.Shared.NextSingle() * 20f - 10f),
                            Radius = Random.Shared.NextSingle() * 3f + 1f,
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
    
    public override void PreRender(double delta, Matrix4x4 view, Matrix4x4 projection)
    {
        AntiAliasing antiAliasing = _renderSettings.AntiAliasing.Get();
        _gl.Set(EnableCap.Multisample, antiAliasing == AntiAliasing.MSAA);
        
        // 1) Depth pre-pass
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _depthFBO);
        _gl.Viewport(0, 0, (uint)_screenWidth, (uint)_screenHeight);
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
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        
        // 2) SSAO pass
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _ssaoFBO);
        _gl.Viewport(0, 0, (uint)_screenWidth, (uint)_screenHeight);
        _gl.Clear(ClearBufferMask.ColorBufferBit);

        _ssaoShader.Activate();
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _depthTex);
        _gl.Uniform1(_gl.GetUniformLocation(_ssaoShader.Handle, "uDepthTex"), 0);
        _ssaoShader.SetUniform("near", near);
        _ssaoShader.SetUniform("far", far);
        _gl.Uniform2(_gl.GetUniformLocation(_ssaoShader.Handle, "uScreenSize"), _screenWidth, _screenHeight);
        
        _ssaoShader.SetUniform("uProj", projection);
        Matrix4x4.Invert(projection, out Matrix4x4 invProj);
        _ssaoShader.SetUniform("uInvProj", invProj);
        
        _gl.Set(EnableCap.CullFace, false);
        _screenVAO.Bind();
        _gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
        _screenVAO.Unbind();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        
        // 2) Prepare lights (transform to view-space & upload)
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

        // upload lights (BufferSubData)
        _gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, _lightsSSBO);
        // pin and upload
        int bytes = numLights * Marshal.SizeOf<GPULight>();
        fixed (GPULight* p = gpuLights)
        {
            _gl.BufferSubData(BufferTargetARB.ShaderStorageBuffer, 0, (nuint)bytes, p);
        }
        
        // 3) Clear tileCounts to zero
        _gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, _tileCountsSSBO);
        // zero the counts via BufferSubData with a zeroed array
        var zeros = new uint[_numTiles];
        fixed (uint* z = zeros) {
            _gl.BufferSubData(BufferTargetARB.ShaderStorageBuffer, 0, (nuint)(zeros.Length * sizeof(uint)), z);
        }
        
        // 4) Dispatch compute shader
        _computeShader.Activate();
        // bind depth texture
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _depthTex);
        _gl.Uniform2(_gl.GetUniformLocation(_computeShader.Handle, "uScreenSize"), _screenWidth, _screenHeight);
        _gl.Uniform2(_gl.GetUniformLocation(_computeShader.Handle, "uTileSize"), TILE_WIDTH, TILE_HEIGHT);
        _gl.Uniform1(_gl.GetUniformLocation(_computeShader.Handle, "uNumLights"), numLights);
        _gl.Uniform1(_gl.GetUniformLocation(_computeShader.Handle, "uMaxLightsPerTile"), MAX_LIGHTS_PER_TILE);
        _gl.Uniform1(_gl.GetUniformLocation(_computeShader.Handle, "uMaxLightViewDistance"), 1000f);
        _computeShader.SetUniform("uInvProj", invProj);
        
        var groupsX = (uint)_numTilesX;
        var groupsY = (uint)_numTilesY;
        _gl.DispatchCompute(groupsX, groupsY, 1);
        
        // 5) memory barrier so SSBO writes are visible to fragment shader reads
        _gl.MemoryBarrier( MemoryBarrierMask.ShaderStorageBarrierBit | MemoryBarrierMask.TextureUpdateBarrierBit);
        
        // 6) Forward shading pass (bind the SSBOs and draw scene)
        _fragmentShader.Activate();
        _fragmentShader.SetUniform("view", view);
        _fragmentShader.SetUniform("projection", projection);
        _fragmentShader.SetUniform("uCameraPos", new Vector3(view.M41, view.M42, view.M43));
        _gl.Uniform2(_gl.GetUniformLocation(_fragmentShader.Handle, "uScreenSize"), _screenWidth, _screenHeight);
        _gl.Uniform2(_gl.GetUniformLocation(_fragmentShader.Handle, "uTileSize"), TILE_WIDTH, TILE_HEIGHT);
        _gl.Uniform1(_gl.GetUniformLocation(_fragmentShader.Handle, "uMaxLightsPerTile"), MAX_LIGHTS_PER_TILE);
        
        // ensure SSBO bindings are set (we already Bound base earlier). But set again to be safe:
        _gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 0, _lightsSSBO);
        _gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 1, _tileIndicesSSBO);
        _gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 2, _tileCountsSSBO);
    }

    private static float[] MatrixToFloatArrayColumnMajor(Matrix4x4 invProj)
    {
        var mat = new float[16];
        mat[0] = invProj.M11; mat[4] = invProj.M12; mat[8] = invProj.M13; mat[12] = invProj.M14;
        mat[1] = invProj.M21; mat[5] = invProj.M22; mat[9] = invProj.M23; mat[13] = invProj.M24;
        mat[2] = invProj.M31; mat[6] = invProj.M32; mat[10] = invProj.M33; mat[14] = invProj.M34;
        mat[3] = invProj.M41; mat[7] = invProj.M42; mat[11] = invProj.M43; mat[15] = invProj.M44;
        return mat;
    }

    public override void PostRender(double delta, Matrix4x4 view, Matrix4x4 projection)
    {
    }
    
    protected override void ShaderActivationCallback(ShaderProgram shader)
    {
        _gl.Uniform2(_gl.GetUniformLocation(shader.Handle, "uScreenSize"), _screenWidth, _screenHeight);
        _gl.Uniform2(_gl.GetUniformLocation(shader.Handle, "uTileSize"), TILE_WIDTH, TILE_HEIGHT);
        _gl.Uniform1(_gl.GetUniformLocation(shader.Handle, "uMaxLightsPerTile"), MAX_LIGHTS_PER_TILE);
        _gl.ActiveTexture(TextureUnit.Texture1);
        _gl.BindTexture(TextureTarget.Texture2D, _ssaoTex);
        shader.SetUniform("uAO", 1);
    }
}