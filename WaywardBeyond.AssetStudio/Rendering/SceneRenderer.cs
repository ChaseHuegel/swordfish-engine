using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using Silk.NET.OpenGL;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Meshing;

namespace WaywardBeyond.AssetStudio.Rendering;

/// <summary>Owns all OpenGL resources and draw calls for unlit 3D asset previews and visual editor overlays.</summary>
public sealed class SceneRenderer : IDisposable
{
    private const int FLOATS_PER_VERTEX = 13;
    private const uint VERTEX_STRIDE_BYTES = FLOATS_PER_VERTEX * sizeof(float);

    private readonly List<GpuMeshEntry> _meshes = [];

    private GL? _gl;
    private uint _program;
    private int _mvpLocation = -1;
    private int _albedoLocation = -1;
    private int _texture2DLocation = -1;
    private int _referenceTexture2DLocation = -1;
    private int _textureModeLocation = -1;
    private int _alphaCutoffLocation = -1;
    private uint _albedoTexture;
    private uint _standaloneTexture;
    private uint _referenceTexture;

    private uint _offscreenFbo;
    private uint _offscreenColorTexture;
    private uint _offscreenDepthRbo;
    private int _offscreenWidth;
    private int _offscreenHeight;

    private bool _disposed;

    /// <summary>True once OpenGL shaders and default textures have been initialized.</summary>
    public bool IsInitialized => _gl != null && _program != 0;

    /// <summary>Reported OpenGL vendor, renderer, and version strings once initialized.</summary>
    public GlInfo? Info { get; private set; }

    /// <summary>Initializes Silk.NET OpenGL bindings, compiles the preview shader, and uploads default textures.</summary>
    public Result<GlInfo> Initialize(Func<string, nint> getProcAddress, bool isOpenGlEs)
    {
        if (getProcAddress == null)
        {
            return Result<GlInfo>.FromFailure("OpenGL procedure loader is null.");
        }

        try
        {
            DisposeGpuResources();
            _disposed = false;

            _gl = GL.GetApi(getProcAddress);
            DrainGlErrors();
            string vendor = _gl.GetStringS(StringName.Vendor) ?? "Unknown";
            string renderer = _gl.GetStringS(StringName.Renderer) ?? "Unknown";
            string version = _gl.GetStringS(StringName.Version) ?? "Unknown";

            Result<uint> programResult = CompileProgram(_gl, isOpenGlEs);
            if (!programResult.Success)
            {
                DisposeGpuResources();
                return Result<GlInfo>.FromFailure(programResult.Message);
            }

            _program = programResult.Value;
            _mvpLocation = _gl.GetUniformLocation(_program, "uMvp");
            _albedoLocation = _gl.GetUniformLocation(_program, "uAlbedo");
            _texture2DLocation = _gl.GetUniformLocation(_program, "uTexture2D");
            _referenceTexture2DLocation = _gl.GetUniformLocation(_program, "uReferenceTexture2D");
            _textureModeLocation = _gl.GetUniformLocation(_program, "uTextureMode");
            _alphaCutoffLocation = _gl.GetUniformLocation(_program, "uAlphaCutoff");

            Result defaultTexResult = UploadAlbedoArray(PbrAlbedoArrayLoader.CreateMissingOnly());
            if (!defaultTexResult.Success)
            {
                DisposeGpuResources();
                return Result<GlInfo>.FromFailure(defaultTexResult.Message);
            }

            StandaloneTextureData missingChecker = StandaloneTextureLoader.CreateMissingCheckerboard();
            Result defaultStandaloneResult = UploadStandaloneTexture(missingChecker);
            if (!defaultStandaloneResult.Success)
            {
                DisposeGpuResources();
                return Result<GlInfo>.FromFailure(defaultStandaloneResult.Message);
            }

            Result defaultReferenceResult = UploadReferenceTexture(missingChecker);
            if (!defaultReferenceResult.Success)
            {
                DisposeGpuResources();
                return Result<GlInfo>.FromFailure(defaultReferenceResult.Message);
            }

            var info = new GlInfo(vendor, renderer, version, isOpenGlEs);
            Info = info;
            return Result<GlInfo>.FromSuccess(info);
        }
        catch (Exception ex)
        {
            DisposeGpuResources();
            return Result<GlInfo>.FromFailure(ex);
        }
    }

    /// <summary>Uploads a decoded albedo texture array into the active <c>GL_TEXTURE_2D_ARRAY</c> binding.</summary>
    public Result UploadAlbedoArray(AlbedoTextureArrayData data)
    {
        if (_gl == null)
        {
            return Result.FromFailure("SceneRenderer is not initialized.");
        }

        if (data == null || data.Width <= 0 || data.Height <= 0 || data.LayerCount <= 0)
        {
            return Result.FromFailure("Albedo texture array data is empty or invalid.");
        }

        try
        {
            DrainGlErrors();
            if (_albedoTexture != 0)
            {
                _gl.DeleteTexture(_albedoTexture);
                _albedoTexture = 0;
            }

            _albedoTexture = _gl.GenTexture();
            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2DArray, _albedoTexture);
            _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);

            ReadOnlySpan<byte> pixels = data.Pixels.AsSpan();
            _gl.TexImage3D(
                TextureTarget.Texture2DArray,
                level: 0,
                internalformat: InternalFormat.Rgba8,
                width: (uint)data.Width,
                height: (uint)data.Height,
                depth: (uint)data.LayerCount,
                border: 0,
                format: PixelFormat.Rgba,
                type: PixelType.UnsignedByte,
                pixels: pixels
            );

            bool isPowerOfTwo = BitOperations.IsPow2(data.Width) && BitOperations.IsPow2(data.Height);
            _gl.TexParameter(
                TextureTarget.Texture2DArray,
                TextureParameterName.TextureMinFilter,
                (int)(isPowerOfTwo ? GLEnum.LinearMipmapLinear : GLEnum.Linear));
            _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
            _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
            _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);
            if (isPowerOfTwo)
            {
                _gl.GenerateMipmap(TextureTarget.Texture2DArray);
            }

            _gl.BindTexture(TextureTarget.Texture2DArray, 0);

            return CheckGlError("UploadAlbedoArray");
        }
        catch (Exception ex)
        {
            return Result.FromFailure(ex);
        }
    }

    /// <summary>Uploads a standalone RGBA8 2D texture bound to texture unit 1 for item and material previews.</summary>
    public Result UploadStandaloneTexture(StandaloneTextureData data)
    {
        return Upload2DTexture(ref _standaloneTexture, data, "UploadStandaloneTexture");
    }

    /// <summary>Uploads an auxiliary RGBA8 2D texture bound to texture unit 2 for body scale reference sprites.</summary>
    public Result UploadReferenceTexture(StandaloneTextureData data)
    {
        return Upload2DTexture(ref _referenceTexture, data, "UploadReferenceTexture");
    }

    /// <summary>Replaces the uploaded scene geometry with <paramref name="meshes"/>.</summary>
    public Result SetSceneMeshes(IReadOnlyList<PreviewSceneMesh> meshes)
    {
        if (_gl == null)
        {
            return Result.FromFailure("SceneRenderer is not initialized.");
        }

        try
        {
            DrainGlErrors();
            ClearSceneMeshes();
            if (meshes == null || meshes.Count == 0)
            {
                return Result.FromSuccess();
            }

            for (var i = 0; i < meshes.Count; i++)
            {
                PreviewSceneMesh item = meshes[i];
                if (item.Geometry.IsEmpty || item.Geometry.Triangles == null || item.Geometry.Triangles.Length == 0)
                {
                    continue;
                }

                GpuMeshEntry entry = UploadMesh(_gl, in item);
                _meshes.Add(entry);
            }

            return CheckGlError("SetSceneMeshes");
        }
        catch (Exception ex)
        {
            return Result.FromFailure(ex);
        }
    }

    /// <summary>Renders the active scene meshes into <paramref name="framebuffer"/> at the requested viewport size.</summary>
    public Result RenderToFramebuffer(
        uint framebuffer,
        int width,
        int height,
        PreviewCamera camera,
        Vector4 clearColor
    ) {
        if (_gl == null || _program == 0)
        {
            return Result.FromFailure("SceneRenderer is not initialized.");
        }

        if (width <= 0 || height <= 0)
        {
            return Result.FromFailure("Viewport dimensions must be positive.");
        }

        try
        {
            DrainGlErrors();
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
            _gl.Viewport(0, 0, (uint)width, (uint)height);
            _gl.ColorMask(true, true, true, true);
            _gl.DepthMask(true);
            _gl.ClearColor(clearColor.X, clearColor.Y, clearColor.Z, clearColor.W);
            _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            if (_meshes.Count == 0)
            {
                return Result.FromSuccess();
            }

            _gl.Enable(EnableCap.DepthTest);
            _gl.DepthFunc(DepthFunction.Less);
            _gl.UseProgram(_program);

            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2DArray, _albedoTexture);
            if (_albedoLocation >= 0)
            {
                _gl.Uniform1(_albedoLocation, 0);
            }

            _gl.ActiveTexture(TextureUnit.Texture1);
            _gl.BindTexture(TextureTarget.Texture2D, _standaloneTexture);
            if (_texture2DLocation >= 0)
            {
                _gl.Uniform1(_texture2DLocation, 1);
            }

            _gl.ActiveTexture(TextureUnit.Texture2);
            _gl.BindTexture(TextureTarget.Texture2D, _referenceTexture);
            if (_referenceTexture2DLocation >= 0)
            {
                _gl.Uniform1(_referenceTexture2DLocation, 2);
            }

            float aspect = (float)width / height;
            Matrix4x4 viewProj = camera.GetViewMatrix() * camera.GetProjectionMatrix(aspect);

            DrawBucket(BrickAlpha.Opaque, isOverlay: false, in viewProj);
            DrawBucket(BrickAlpha.Masked, isOverlay: false, in viewProj);
            DrawBucket(BrickAlpha.Blend, isOverlay: false, in viewProj);

            if (HasOverlayMeshes())
            {
                _gl.DepthMask(true);
                _gl.Clear(ClearBufferMask.DepthBufferBit);
                DrawBucket(BrickAlpha.Opaque, isOverlay: true, in viewProj);
                DrawBucket(BrickAlpha.Masked, isOverlay: true, in viewProj);
                DrawBucket(BrickAlpha.Blend, isOverlay: true, in viewProj);
            }

            _gl.BindVertexArray(0);
            _gl.ActiveTexture(TextureUnit.Texture2);
            _gl.BindTexture(TextureTarget.Texture2D, 0);
            _gl.ActiveTexture(TextureUnit.Texture1);
            _gl.BindTexture(TextureTarget.Texture2D, 0);
            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2DArray, 0);
            _gl.UseProgram(0);
            _gl.Disable(EnableCap.Blend);
            _gl.Disable(EnableCap.CullFace);
            _gl.DepthMask(true);

            return CheckGlError("RenderToFramebuffer");
        }
        catch (Exception ex)
        {
            return Result.FromFailure(ex);
        }
    }

    /// <summary>Renders the active scene into an offscreen framebuffer and returns top-down RGBA8 pixels.</summary>
    public Result<byte[]> RenderToRgba(int width, int height, PreviewCamera camera, Vector4 clearColor)
    {
        if (_gl == null || _program == 0)
        {
            return Result<byte[]>.FromFailure("SceneRenderer is not initialized.");
        }

        if (width <= 0 || height <= 0)
        {
            return Result<byte[]>.FromFailure("Offscreen dimensions must be positive.");
        }

        Result fboResult = EnsureOffscreenTarget(width, height);
        if (!fboResult.Success)
        {
            return Result<byte[]>.FromFailure(fboResult.Message);
        }

        Result renderResult = RenderToFramebuffer(_offscreenFbo, width, height, camera, clearColor);
        if (!renderResult.Success)
        {
            return Result<byte[]>.FromFailure(renderResult.Message);
        }

        try
        {
            int byteCount = width * height * 4;
            var raw = new byte[byteCount];
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _offscreenFbo);
            _gl.PixelStore(PixelStoreParameter.PackAlignment, 1);
            DrainGlErrors();
            _gl.ReadPixels(0, 0, (uint)width, (uint)height, PixelFormat.Rgba, PixelType.UnsignedByte, raw.AsSpan());
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            Result errorCheck = CheckGlError("ReadPixels");
            if (!errorCheck.Success)
            {
                return Result<byte[]>.FromFailure(errorCheck.Message);
            }

            // Flip OpenGL bottom-up rows into standard top-down image order.
            var topDown = new byte[byteCount];
            int rowBytes = width * 4;
            for (var y = 0; y < height; y++)
            {
                int srcRow = height - 1 - y;
                System.Buffer.BlockCopy(raw, srcRow * rowBytes, topDown, y * rowBytes, rowBytes);
            }

            return Result<byte[]>.FromSuccess(topDown);
        }
        catch (Exception ex)
        {
            return Result<byte[]>.FromFailure(ex);
        }
    }

    /// <summary>Deletes OpenGL resources while the context is still current so the renderer can be re-initialized later.</summary>
    public void Deinitialize()
    {
        DisposeGpuResources();
    }

    /// <summary>Clears cached GPU handles without issuing OpenGL calls after the host context has already been lost.</summary>
    public void HandleContextLost()
    {
        _meshes.Clear();
        _offscreenFbo = 0;
        _offscreenColorTexture = 0;
        _offscreenDepthRbo = 0;
        _offscreenWidth = 0;
        _offscreenHeight = 0;
        _albedoTexture = 0;
        _standaloneTexture = 0;
        _referenceTexture = 0;
        _program = 0;
        _mvpLocation = -1;
        _albedoLocation = -1;
        _texture2DLocation = -1;
        _referenceTexture2DLocation = -1;
        _textureModeLocation = -1;
        _alphaCutoffLocation = -1;

        _gl?.Dispose();
        _gl = null;
        Info = null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DisposeGpuResources();
    }

    private Result Upload2DTexture(ref uint textureHandle, StandaloneTextureData data, string operationName)
    {
        if (_gl == null)
        {
            return Result.FromFailure("SceneRenderer is not initialized.");
        }

        if (!data.IsValid)
        {
            return Result.FromFailure("Standalone texture data is empty or invalid.");
        }

        try
        {
            DrainGlErrors();
            if (textureHandle != 0)
            {
                _gl.DeleteTexture(textureHandle);
                textureHandle = 0;
            }

            textureHandle = _gl.GenTexture();
            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, textureHandle);
            _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);

            ReadOnlySpan<byte> pixels = data.RgbaPixels.AsSpan();
            _gl.TexImage2D(
                TextureTarget.Texture2D,
                level: 0,
                internalformat: InternalFormat.Rgba8,
                width: (uint)data.Width,
                height: (uint)data.Height,
                border: 0,
                format: PixelFormat.Rgba,
                type: PixelType.UnsignedByte,
                pixels: pixels
            );

            bool isPowerOfTwo = BitOperations.IsPow2(data.Width) && BitOperations.IsPow2(data.Height);
            _gl.TexParameter(
                TextureTarget.Texture2D,
                TextureParameterName.TextureMinFilter,
                (int)(isPowerOfTwo ? GLEnum.LinearMipmapLinear : GLEnum.Linear));
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
            if (isPowerOfTwo)
            {
                _gl.GenerateMipmap(TextureTarget.Texture2D);
            }

            _gl.BindTexture(TextureTarget.Texture2D, 0);
            return CheckGlError(operationName);
        }
        catch (Exception ex)
        {
            return Result.FromFailure(ex);
        }
    }

    private bool HasOverlayMeshes()
    {
        for (var i = 0; i < _meshes.Count; i++)
        {
            if (_meshes[i].IsOverlay)
            {
                return true;
            }
        }

        return false;
    }

    private void DrawBucket(BrickAlpha bucket, bool isOverlay, in Matrix4x4 viewProj)
    {
        GL gl = _gl!;
        bool isBlend = bucket == BrickAlpha.Blend;
        if (isBlend)
        {
            gl.Enable(EnableCap.Blend);
            gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            gl.DepthMask(false);
        }
        else
        {
            gl.Disable(EnableCap.Blend);
            gl.DepthMask(true);
        }

        Span<float> mvpData = stackalloc float[16];
        for (var i = 0; i < _meshes.Count; i++)
        {
            GpuMeshEntry entry = _meshes[i];
            if (entry.IsOverlay != isOverlay || entry.AlphaMode != bucket)
            {
                continue;
            }

            if (entry.DoubleSided)
            {
                gl.Disable(EnableCap.CullFace);
            }
            else
            {
                gl.Enable(EnableCap.CullFace);
                gl.CullFace(TriangleFace.Back);
                gl.FrontFace(FrontFaceDirection.Ccw);
            }

            if (_alphaCutoffLocation >= 0)
            {
                float cutoff = entry.AlphaMode == BrickAlpha.Masked ? entry.AlphaCutoff : 0f;
                gl.Uniform1(_alphaCutoffLocation, cutoff);
            }

            if (_textureModeLocation >= 0)
            {
                gl.Uniform1(_textureModeLocation, (int)entry.TextureMode);
            }

            if (_mvpLocation >= 0)
            {
                Matrix4x4 mvp = entry.Model * viewProj;
                CopyMatrixToSpan(in mvp, mvpData);
                gl.UniformMatrix4(_mvpLocation, transpose: false, mvpData);
            }

            gl.BindVertexArray(entry.Vao);
            gl.DrawElements(PrimitiveType.Triangles, entry.IndexCount, DrawElementsType.UnsignedInt, in Unsafe.NullRef<byte>());
        }
    }

    private Result EnsureOffscreenTarget(int width, int height)
    {
        GL gl = _gl!;
        if (_offscreenFbo != 0 && _offscreenWidth == width && _offscreenHeight == height)
        {
            return Result.FromSuccess();
        }

        DeleteOffscreenTarget();

        _offscreenFbo = gl.GenFramebuffer();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _offscreenFbo);

        _offscreenColorTexture = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, _offscreenColorTexture);
        gl.TexImage2D(
            TextureTarget.Texture2D,
            level: 0,
            internalformat: InternalFormat.Rgba8,
            width: (uint)width,
            height: (uint)height,
            border: 0,
            format: PixelFormat.Rgba,
            type: PixelType.UnsignedByte,
            pixels: ReadOnlySpan<byte>.Empty
        );
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        gl.FramebufferTexture2D(
            FramebufferTarget.Framebuffer,
            FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D,
            _offscreenColorTexture,
            level: 0
        );

        _offscreenDepthRbo = gl.GenRenderbuffer();
        gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _offscreenDepthRbo);
        gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.DepthComponent24, (uint)width, (uint)height);
        gl.FramebufferRenderbuffer(
            FramebufferTarget.Framebuffer,
            FramebufferAttachment.DepthAttachment,
            RenderbufferTarget.Renderbuffer,
            _offscreenDepthRbo
        );

        GLEnum status = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        gl.BindTexture(TextureTarget.Texture2D, 0);
        gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);

        if (status != GLEnum.FramebufferComplete)
        {
            DeleteOffscreenTarget();
            return Result.FromFailure($"Offscreen framebuffer incomplete: {status}.");
        }

        _offscreenWidth = width;
        _offscreenHeight = height;
        return Result.FromSuccess();
    }

    private static GpuMeshEntry UploadMesh(GL gl, in PreviewSceneMesh item)
    {
        VoxelMesh geom = item.Geometry;
        int vertexCount = geom.Vertices.Length;
        var interleaved = new float[vertexCount * FLOATS_PER_VERTEX];

        for (var i = 0; i < vertexCount; i++)
        {
            int o = i * FLOATS_PER_VERTEX;
            Vector3 p = geom.Vertices[i];
            Vector4 c = i < geom.Colors.Length ? geom.Colors[i] : Vector4.One;
            Vector3 uv = i < geom.Uv.Length ? geom.Uv[i] : Vector3.Zero;
            Vector3 n = i < geom.Normals.Length ? geom.Normals[i] : Vector3.UnitY;

            interleaved[o] = p.X;
            interleaved[o + 1] = p.Y;
            interleaved[o + 2] = p.Z;
            interleaved[o + 3] = c.X;
            interleaved[o + 4] = c.Y;
            interleaved[o + 5] = c.Z;
            interleaved[o + 6] = c.W;
            interleaved[o + 7] = uv.X;
            interleaved[o + 8] = uv.Y;
            interleaved[o + 9] = uv.Z;
            interleaved[o + 10] = n.X;
            interleaved[o + 11] = n.Y;
            interleaved[o + 12] = n.Z;
        }

        uint vao = gl.GenVertexArray();
        uint vbo = gl.GenBuffer();
        uint ebo = gl.GenBuffer();

        gl.BindVertexArray(vao);

        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        gl.BufferData(BufferTargetARB.ArrayBuffer, (ReadOnlySpan<float>)interleaved, BufferUsageARB.StaticDraw);

        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebo);
        gl.BufferData(BufferTargetARB.ElementArrayBuffer, (ReadOnlySpan<uint>)geom.Triangles, BufferUsageARB.StaticDraw);

        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, VERTEX_STRIDE_BYTES, 0);

        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, VERTEX_STRIDE_BYTES, 3 * sizeof(float));

        gl.EnableVertexAttribArray(2);
        gl.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, VERTEX_STRIDE_BYTES, 7 * sizeof(float));

        gl.EnableVertexAttribArray(3);
        gl.VertexAttribPointer(3, 3, VertexAttribPointerType.Float, false, VERTEX_STRIDE_BYTES, 10 * sizeof(float));

        gl.BindVertexArray(0);

        return new GpuMeshEntry(
            vao,
            vbo,
            ebo,
            (uint)geom.Triangles.Length,
            item.Model,
            item.AlphaMode,
            item.AlphaCutoff,
            item.DoubleSided,
            item.TextureMode,
            item.IsOverlay);
    }

    private static Result<uint> CompileProgram(GL gl, bool isOpenGlEs)
    {
        uint vertexShader = gl.CreateShader(ShaderType.VertexShader);
        uint fragmentShader = gl.CreateShader(ShaderType.FragmentShader);
        uint program = 0;

        try
        {
            gl.ShaderSource(vertexShader, PreviewShaderSource.GetVertexShader(isOpenGlEs));
            gl.CompileShader(vertexShader);
            gl.GetShader(vertexShader, ShaderParameterName.CompileStatus, out int vStatus);
            if (vStatus == 0)
            {
                string log = gl.GetShaderInfoLog(vertexShader);
                return Result<uint>.FromFailure($"Vertex shader compilation failed: {log}");
            }

            gl.ShaderSource(fragmentShader, PreviewShaderSource.GetFragmentShader(isOpenGlEs));
            gl.CompileShader(fragmentShader);
            gl.GetShader(fragmentShader, ShaderParameterName.CompileStatus, out int fStatus);
            if (fStatus == 0)
            {
                string log = gl.GetShaderInfoLog(fragmentShader);
                return Result<uint>.FromFailure($"Fragment shader compilation failed: {log}");
            }

            program = gl.CreateProgram();
            gl.AttachShader(program, vertexShader);
            gl.AttachShader(program, fragmentShader);
            gl.LinkProgram(program);
            gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out int linkStatus);
            if (linkStatus == 0)
            {
                string log = gl.GetProgramInfoLog(program);
                gl.DeleteProgram(program);
                return Result<uint>.FromFailure($"Shader link failed: {log}");
            }

            return Result<uint>.FromSuccess(program);
        }
        finally
        {
            gl.DeleteShader(vertexShader);
            gl.DeleteShader(fragmentShader);
        }
    }

    private Result CheckGlError(string operation)
    {
        if (_gl == null)
        {
            return Result.FromSuccess();
        }

        GLEnum firstError = _gl.GetError();
        if (firstError == GLEnum.NoError)
        {
            return Result.FromSuccess();
        }

        DrainGlErrors();
        return Result.FromFailure($"OpenGL error during {operation}: {firstError}.");
    }

    private void DrainGlErrors()
    {
        if (_gl == null)
        {
            return;
        }

        for (var i = 0; i < 16 && _gl.GetError() != GLEnum.NoError; i++)
        {
        }
    }

    private void ClearSceneMeshes()
    {
        if (_gl == null)
        {
            _meshes.Clear();
            return;
        }

        for (var i = 0; i < _meshes.Count; i++)
        {
            GpuMeshEntry entry = _meshes[i];
            _gl.DeleteVertexArray(entry.Vao);
            _gl.DeleteBuffer(entry.Vbo);
            _gl.DeleteBuffer(entry.Ebo);
        }

        _meshes.Clear();
    }

    private void DeleteOffscreenTarget()
    {
        if (_gl == null)
        {
            return;
        }

        if (_offscreenFbo != 0)
        {
            _gl.DeleteFramebuffer(_offscreenFbo);
            _offscreenFbo = 0;
        }

        if (_offscreenColorTexture != 0)
        {
            _gl.DeleteTexture(_offscreenColorTexture);
            _offscreenColorTexture = 0;
        }

        if (_offscreenDepthRbo != 0)
        {
            _gl.DeleteRenderbuffer(_offscreenDepthRbo);
            _offscreenDepthRbo = 0;
        }

        _offscreenWidth = 0;
        _offscreenHeight = 0;
    }

    private void DisposeGpuResources()
    {
        if (_gl == null)
        {
            return;
        }

        ClearSceneMeshes();
        DeleteOffscreenTarget();

        if (_albedoTexture != 0)
        {
            _gl.DeleteTexture(_albedoTexture);
            _albedoTexture = 0;
        }

        if (_standaloneTexture != 0)
        {
            _gl.DeleteTexture(_standaloneTexture);
            _standaloneTexture = 0;
        }

        if (_referenceTexture != 0)
        {
            _gl.DeleteTexture(_referenceTexture);
            _referenceTexture = 0;
        }

        if (_program != 0)
        {
            _gl.DeleteProgram(_program);
            _program = 0;
        }

        _mvpLocation = -1;
        _albedoLocation = -1;
        _texture2DLocation = -1;
        _referenceTexture2DLocation = -1;
        _textureModeLocation = -1;
        _alphaCutoffLocation = -1;

        _gl.Dispose();
        _gl = null;
        Info = null;
    }

    private static void CopyMatrixToSpan(in Matrix4x4 m, Span<float> dst)
    {
        dst[0] = m.M11;
        dst[1] = m.M12;
        dst[2] = m.M13;
        dst[3] = m.M14;
        dst[4] = m.M21;
        dst[5] = m.M22;
        dst[6] = m.M23;
        dst[7] = m.M24;
        dst[8] = m.M31;
        dst[9] = m.M32;
        dst[10] = m.M33;
        dst[11] = m.M34;
        dst[12] = m.M41;
        dst[13] = m.M42;
        dst[14] = m.M43;
        dst[15] = m.M44;
    }

    private readonly struct GpuMeshEntry(
        uint vao,
        uint vbo,
        uint ebo,
        uint indexCount,
        Matrix4x4 model,
        BrickAlpha alphaMode,
        float alphaCutoff,
        bool doubleSided,
        PreviewTextureMode textureMode,
        bool isOverlay
    ) {
        public readonly uint Vao = vao;
        public readonly uint Vbo = vbo;
        public readonly uint Ebo = ebo;
        public readonly uint IndexCount = indexCount;
        public readonly Matrix4x4 Model = model;
        public readonly BrickAlpha AlphaMode = alphaMode;
        public readonly float AlphaCutoff = alphaCutoff;
        public readonly bool DoubleSided = doubleSided;
        public readonly PreviewTextureMode TextureMode = textureMode;
        public readonly bool IsOverlay = isOverlay;
    }
}
