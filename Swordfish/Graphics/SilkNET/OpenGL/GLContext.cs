using System.Numerics;
using Silk.NET.OpenGL;
using Swordfish.Library.Extensions;
using Swordfish.Library.Types;
using Swordfish.Types;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal unsafe partial class GLContext(in GL gl, in SynchronizationContext synchronizationContext)
{
    private readonly GL _gl = gl;
    private readonly SynchronizationContext _glThread = synchronizationContext;

    public ShaderComponent CreateShaderComponent(string name, Silk.NET.OpenGL.ShaderType type, string source)
    {
        return _glThread.WaitForResult(ShaderComponentArgs.Factory, new ShaderComponentArgs(_gl, name, type, source));
    }

    public ShaderProgram CreateShaderProgram(string name, ShaderComponent[] shaderComponents)
    {
        return _glThread.WaitForResult(ShaderProgramArgs.Factory, new ShaderProgramArgs(_gl, name, shaderComponents));
    }

    public TexImage2D CreateTexImage2D(string name, byte[] pixels, uint width, uint height, TextureFormat format, TextureParams @params)
    {
        fixed (byte* pixelPtr = pixels)
        {
            return _glThread.WaitForResult(TextureArgs.Factory, new TextureArgs(_gl, name, pixelPtr, width, height, format, @params));
        }
    }

    public TexImage3D CreateTexImage3D(string name, byte[] pixels, uint width, uint height, uint depth, TextureFormat format, TextureParams @params)
    {
        fixed (byte* pixelPtr = pixels)
        {
            return _glThread.WaitForResult(TextureArrayArgs.Factory, new TextureArrayArgs(_gl, name, pixelPtr, width, height, depth, format, @params));
        }
    }

    public unsafe TexCubemap CreateTexCubemap(string name, byte[][] pixels, uint width, uint height, TextureFormat format, TextureParams @params)
    {
        if (pixels.Length != 6)
        {
            throw new ArgumentException("Cubemaps require 6 textures.", nameof(pixels));
        }

        fixed (byte* p0 = pixels[0])
        fixed (byte* p1 = pixels[1])
        fixed (byte* p2 = pixels[2])
        fixed (byte* p3 = pixels[3])
        fixed (byte* p4 = pixels[4])
        fixed (byte* p5 = pixels[5])
        {
            byte*[] pixelPtrs = [p0, p1, p2, p3, p4, p5];
            return _glThread.WaitForResult(TextureCubemapArgs.Factory, new TextureCubemapArgs(_gl, name, pixelPtrs, width, height, format, @params));
        }
    }

    public GLMaterial CreateGLMaterial(ShaderProgram shaderProgram, IGLTexture[] textures, bool transparent)
    {
        return _glThread.WaitForResult(GLMaterialArgs.Factory, new GLMaterialArgs(shaderProgram, textures, transparent));
    }

    internal GLRenderTarget CreateGLRenderTarget(int entity, VertexArrayObject<float, uint> vertexArrayObject, BufferObject<Matrix4x4> modelsBufferObject, GLMaterial[] materials, RenderOptions renderOptions)
    {
        return _glThread.WaitForResult(GLRenderTargetArgs.Factory, new GLRenderTargetArgs(_gl, entity, vertexArrayObject, modelsBufferObject, materials, renderOptions));
    }
    
    internal GLRectRenderTarget CreateGLRectRenderTarget(Rect2 rect, Vector4 color, GLMaterial[] materials)
    {
        return _glThread.WaitForResult(GLRectRenderTargetArgs.Factory, new GLRectRenderTargetArgs(rect, color, materials));
    }

    internal VertexArrayObject<TVertexType> CreateVertexArrayObject<TVertexType>(TVertexType[] vertexData)
        where TVertexType : unmanaged
    {
        return _glThread.WaitForResult(VertexArrayObjectArgs<TVertexType>.Factory, new VertexArrayObjectArgs<TVertexType>(_gl, vertexData));
    }

    internal VertexArrayObject<TVertexType, TElementType> CreateVertexArrayObject<TVertexType, TElementType>(TVertexType[] vertexData, TElementType[] indices)
        where TVertexType : unmanaged
        where TElementType : unmanaged
    {
        return _glThread.WaitForResult(VertexArrayObjectArgs<TVertexType, TElementType>.Factory, new VertexArrayObjectArgs<TVertexType, TElementType>(_gl, vertexData, indices));
    }

    internal VertexArrayObject32 CreateVertexArrayObject32(float[] vertexData, uint[] indices)
    {
        return _glThread.WaitForResult(VertexArrayObject32Args.Factory, new VertexArrayObject32Args(_gl, vertexData, indices));
    }

    internal BufferObject<TData> CreateBufferObject<TData>(TData[] data, BufferTargetARB bufferType, BufferUsageARB usage = BufferUsageARB.StaticDraw)
        where TData : unmanaged
    {
        return _glThread.WaitForResult(BufferObjectArgs<TData>.Factory, new BufferObjectArgs<TData>(_gl, data, bufferType, usage));
    }

    private readonly unsafe record struct TextureCubemapArgs(
        GL GL,
        string Name,
        byte*[] Pixels,
        uint Width,
        uint Height,
        TextureFormat Format,
        TextureParams Params)
    {
        public static readonly Func<TextureCubemapArgs, TexCubemap> Factory = args =>
            new(args.GL, args.Name, args.Pixels, args.Width, args.Height, args.Format, args.Params);
    }
}