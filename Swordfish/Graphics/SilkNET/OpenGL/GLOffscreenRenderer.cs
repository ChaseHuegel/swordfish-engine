using System.Numerics;
using Silk.NET.OpenGL;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Extensions;
using Swordfish.Library.Util;

namespace Swordfish.Graphics.SilkNET.OpenGL;

/// <summary>OpenGL implementation of offscreen mesh-to-texture rendering.</summary>
internal sealed unsafe class GLOffscreenRenderer(
    in GL gl,
    in GLRenderContext renderContext,
    in SynchronizationContext synchronizationContext
) : IOffscreenRenderer {
    private const int RGBA_STRIDE_BYTES = 4;

    private readonly GL _gl = gl;
    private readonly GLRenderContext _renderContext = renderContext;
    private readonly SynchronizationContext _synchronizationContext = synchronizationContext;

    /// <inheritdoc/>
    public Result<Texture> RenderToTexture(in OffscreenRenderRequest request)
    {
        Result<Texture>[] results = RenderBatchToTextures([request]);
        return results[0];
    }

    /// <inheritdoc/>
    public Result<Texture>[] RenderBatchToTextures(ReadOnlySpan<OffscreenRenderRequest> requests)
    {
        if (requests.Length == 0)
        {
            return [];
        }

        OffscreenRenderRequest[] requestArray = requests.ToArray();
        RawRenderResult[] rawResults = _synchronizationContext.WaitForResult(RenderBatchOnGLThread, requestArray);

        var results = new Result<Texture>[requestArray.Length];
        for (var i = 0; i < requestArray.Length; i++)
        {
            RawRenderResult raw = rawResults[i];
            if (!raw.Success || raw.Pixels == null)
            {
                results[i] = Result<Texture>.FromFailure(raw.Error ?? "Offscreen render failed.");
                continue;
            }

            OffscreenRenderRequest request = requestArray[i];
            byte[] flipped = FlipRowsVertical(raw.Pixels, request.Width, request.Height, RGBA_STRIDE_BYTES);
            var texture = new Texture(
                request.Name,
                flipped,
                request.Width,
                request.Height,
                request.Mipmaps,
                request.ColorSpace,
                request.Filter
            );
            results[i] = Result<Texture>.FromSuccess(texture);
        }

        return results;
    }

    /// <summary>Flips tightly-packed pixel rows vertically to convert OpenGL bottom-up readback to top-down.</summary>
    internal static byte[] FlipRowsVertical(byte[] pixels, int width, int height, int bytesPerPixel)
    {
        int stride = width * bytesPerPixel;
        var flipped = new byte[pixels.Length];
        for (var y = 0; y < height; y++)
        {
            Array.Copy(
                sourceArray: pixels,
                sourceIndex: y * stride,
                destinationArray: flipped,
                destinationIndex: (height - 1 - y) * stride,
                length: stride
            );
        }

        return flipped;
    }

    private RawRenderResult[] RenderBatchOnGLThread(OffscreenRenderRequest[] requests)
    {
        var results = new RawRenderResult[requests.Length];

        _gl.GetInteger(GetPName.DrawFramebufferBinding, out int previousDrawFbo);
        _gl.GetInteger(GetPName.ReadFramebufferBinding, out int previousReadFbo);
        Span<int> previousViewport = stackalloc int[4];
        _gl.GetInteger(GetPName.Viewport, previousViewport);
        Span<float> previousClearColor = stackalloc float[4];
        _gl.GetFloat(GetPName.ColorClearValue, previousClearColor);
        _gl.GetInteger(GetPName.PackAlignment, out int previousPackAlignment);
        _gl.GetInteger(GetPName.BlendSrcRgb, out int previousBlendSrcRgb);
        _gl.GetInteger(GetPName.BlendDstRgb, out int previousBlendDstRgb);
        _gl.GetInteger(GetPName.BlendSrcAlpha, out int previousBlendSrcAlpha);
        _gl.GetInteger(GetPName.BlendDstAlpha, out int previousBlendDstAlpha);
        bool previousBlend = _gl.IsEnabled(EnableCap.Blend);
        bool previousCullFace = _gl.IsEnabled(EnableCap.CullFace);
        bool previousDepthTest = _gl.IsEnabled(EnableCap.DepthTest);
        _gl.GetBoolean(GetPName.DepthWritemask, out bool previousDepthMask);
        _gl.GetInteger(GetPName.MaxSamples, out int maxSamples);
        maxSamples = Math.Max(1, maxSamples);

        var modelBuffer = new BufferObject<Matrix4x4>(_gl, [Matrix4x4.Identity], BufferTargetARB.ArrayBuffer, BufferUsageARB.DynamicDraw);
        OffscreenTarget? target = null;

        try
        {
            for (var i = 0; i < requests.Length; i++)
            {
                ref readonly OffscreenRenderRequest request = ref requests[i];
                if (!ValidateRequest(in request, out string? error))
                {
                    results[i] = RawRenderResult.Failure(error!);
                    continue;
                }

                int effectiveSamples = Math.Clamp(request.Samples, 1, maxSamples);
                if (target == null || !target.Matches(request.Width, request.Height, effectiveSamples))
                {
                    target?.Dispose();
                    _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                    target = new OffscreenTarget(_gl, (uint)request.Width, (uint)request.Height, (uint)effectiveSamples);
                }

                results[i] = RenderSingle(in request, target, modelBuffer);
            }
        }
        finally
        {
            target?.Dispose();
            modelBuffer.Dispose();

            _gl.BindVertexArray(0);
            _gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, (uint)previousDrawFbo);
            _gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, (uint)previousReadFbo);
            _gl.Viewport(previousViewport[0], previousViewport[1], (uint)previousViewport[2], (uint)previousViewport[3]);
            _gl.ClearColor(previousClearColor[0], previousClearColor[1], previousClearColor[2], previousClearColor[3]);
            _gl.PixelStore(PixelStoreParameter.PackAlignment, previousPackAlignment);
            _gl.BlendFuncSeparate(
                (BlendingFactor)previousBlendSrcRgb,
                (BlendingFactor)previousBlendDstRgb,
                (BlendingFactor)previousBlendSrcAlpha,
                (BlendingFactor)previousBlendDstAlpha
            );
            _gl.Set(EnableCap.Blend, previousBlend);
            _gl.Set(EnableCap.CullFace, previousCullFace);
            _gl.Set(EnableCap.DepthTest, previousDepthTest);
            _gl.DepthMask(previousDepthMask);
        }

        return results;
    }

    private RawRenderResult RenderSingle(in OffscreenRenderRequest request, OffscreenTarget target, BufferObject<Matrix4x4> modelBuffer)
    {
        GLMaterial glMaterial = _renderContext.BindMaterial(request.Material);

        ReadOnlySpan<Matrix4x4> modelSpan = [request.Model];
        modelBuffer.UploadOrResize(modelSpan);

        var vbo = new BufferObject<float>(_gl, request.Mesh.GetRawVertexData(), BufferTargetARB.ArrayBuffer);
        var ebo = new BufferObject<uint>(_gl, request.Mesh.Triangles, BufferTargetARB.ElementArrayBuffer);
        using var vao = new VertexArrayObject32(_gl, vbo, ebo);

        vao.Bind();
        vao.VertexBufferObject.Bind();
        vao.SetVertexAttribute(0, 3, VertexAttribPointerType.Float, GLRenderTarget.VERTEX_DATA_LENGTH, 0);
        vao.SetVertexAttribute(1, 4, VertexAttribPointerType.Float, GLRenderTarget.VERTEX_DATA_LENGTH, 3);
        vao.SetVertexAttribute(2, 3, VertexAttribPointerType.Float, GLRenderTarget.VERTEX_DATA_LENGTH, 7);
        vao.SetVertexAttribute(3, 3, VertexAttribPointerType.Float, GLRenderTarget.VERTEX_DATA_LENGTH, 10);

        modelBuffer.Bind();
        for (uint row = 0; row < 4; row++)
        {
            vao.SetVertexAttributePointer(4 + row, 4, VertexAttribPointerType.Float, (uint)sizeof(Matrix4x4), (int)(row * sizeof(float) * 4));
            vao.SetVertexAttributeDivisor(4 + row, 1);
        }

        target.RenderFbo.Bind();
        _gl.ClearColor(request.ClearColor.X, request.ClearColor.Y, request.ClearColor.Z, request.ClearColor.W);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        _gl.Enable(EnableCap.DepthTest);
        _gl.DepthMask(true);
        _gl.Set(EnableCap.CullFace, !request.RenderOptions.DoubleFaced);

        bool useBlend = request.RenderOptions.AlphaMode == AlphaMode.Blend || glMaterial.Transparent;
        if (useBlend)
        {
            _gl.Enable(EnableCap.Blend);
            _gl.BlendFuncSeparate(
                BlendingFactor.SrcAlpha,
                BlendingFactor.OneMinusSrcAlpha,
                BlendingFactor.One,
                BlendingFactor.OneMinusSrcAlpha
            );
        }
        else
        {
            _gl.Disable(EnableCap.Blend);
        }

        using (glMaterial.Use())
        {
            glMaterial.ShaderProgram.SetUniform("view", request.View);
            glMaterial.ShaderProgram.SetUniform("projection", request.Projection);
            glMaterial.ShaderProgram.SetUniform(
                "uAlphaCutoff",
                request.RenderOptions.AlphaMode == AlphaMode.Masked ? request.RenderOptions.AlphaCutoff : 0f
            );

            vao.Bind();
            _gl.DrawElementsInstanced(
                PrimitiveType.Triangles,
                (uint)request.Mesh.Triangles.Length,
                DrawElementsType.UnsignedInt,
                (void*)0,
                1
            );
        }

        _gl.BindVertexArray(0);

        if (target.NeedsResolve)
        {
            target.RenderFbo.Blit(
                target.ResolveFbo,
                ReadBufferMode.ColorAttachment0,
                DrawBufferMode.ColorAttachment0,
                ClearBufferMask.ColorBufferBit,
                BlitFramebufferFilter.Nearest
            );
        }

        var pixels = new byte[request.Width * request.Height * RGBA_STRIDE_BYTES];
        using (target.ResolveFbo.Use(FramebufferTarget.ReadFramebuffer, ReadBufferMode.ColorAttachment0))
        {
            _gl.PixelStore(PixelStoreParameter.PackAlignment, 1);
            fixed (byte* ptr = pixels)
            {
                _gl.ReadPixels(0, 0, (uint)request.Width, (uint)request.Height, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
            }
        }

        return RawRenderResult.FromSuccess(pixels);
    }

    private static bool ValidateRequest(in OffscreenRenderRequest request, out string? error)
    {
        if (request.Width <= 0 || request.Height <= 0)
        {
            error = $"Offscreen render '{request.Name}' has invalid dimensions {request.Width}x{request.Height}.";
            return false;
        }

        if (request.Mesh == null || request.Mesh.IsDisposed || request.Mesh.Triangles.Length == 0)
        {
            error = $"Offscreen render '{request.Name}' has an empty or disposed mesh.";
            return false;
        }

        if (request.Material == null || request.Material.IsDisposed)
        {
            error = $"Offscreen render '{request.Name}' has a null or disposed material.";
            return false;
        }

        error = null;
        return true;
    }

    private readonly struct RawRenderResult(bool success, byte[]? pixels, string? error)
    {
        public readonly bool Success = success;
        public readonly byte[]? Pixels = pixels;
        public readonly string? Error = error;

        public static RawRenderResult FromSuccess(byte[] pixels) => new(true, pixels, null);
        public static RawRenderResult Failure(string error) => new(false, null, error);
    }

    private sealed class OffscreenTarget : IDisposable
    {
        public readonly FramebufferObject RenderFbo;
        public readonly FramebufferObject ResolveFbo;
        public readonly bool NeedsResolve;

        private readonly uint _width;
        private readonly uint _height;
        private readonly uint _samples;
        private readonly TexImage2D _resolveTexture;
        private readonly RenderbufferObject? _colorRbo;
        private readonly RenderbufferObject _depthRbo;

        public OffscreenTarget(GL gl, uint width, uint height, uint samples)
        {
            _width = width;
            _height = height;
            _samples = samples;
            NeedsResolve = samples > 1;

            _resolveTexture = new TexImage2D(gl, "offscreen_resolve", pixels: null, width, height, TextureFormat.Rgba, TextureParams.ClampLinear);

            if (NeedsResolve)
            {
                _colorRbo = new RenderbufferObject(gl, "offscreen_color", width, height, FramebufferAttachment.ColorAttachment0, InternalFormat.Rgba8, samples);
                _depthRbo = new RenderbufferObject(gl, "offscreen_depth", width, height, FramebufferAttachment.DepthStencilAttachment, InternalFormat.Depth24Stencil8, samples);
                RenderFbo = new FramebufferObject(gl, "offscreen_render", width, height, [DrawBufferMode.ColorAttachment0], [_colorRbo, _depthRbo]);
                ResolveFbo = new FramebufferObject(gl, "offscreen_resolve", _resolveTexture, FramebufferAttachment.ColorAttachment0);
            }
            else
            {
                _depthRbo = new RenderbufferObject(gl, "offscreen_depth", width, height, FramebufferAttachment.DepthStencilAttachment, InternalFormat.Depth24Stencil8, samples: null);
                ResolveFbo = new FramebufferObject(gl, "offscreen_resolve", _resolveTexture, FramebufferAttachment.ColorAttachment0);
                using (ResolveFbo.Use())
                using (_depthRbo.Use())
                {
                    gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, _depthRbo.Handle);
                    GLEnum status = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
                    if (status != GLEnum.FramebufferComplete)
                    {
                        throw new FatalAlertException($"Framebuffer \"offscreen_resolve\" is incomplete ({status}).");
                    }
                }

                RenderFbo = ResolveFbo;
            }
        }

        public bool Matches(int width, int height, int samples)
        {
            return _width == (uint)width && _height == (uint)height && _samples == (uint)samples;
        }

        public void Dispose()
        {
            if (NeedsResolve)
            {
                RenderFbo.Dispose();
                _colorRbo?.Dispose();
            }

            ResolveFbo.Dispose();
            _depthRbo.Dispose();
            _resolveTexture.Dispose();
        }
    }
}
