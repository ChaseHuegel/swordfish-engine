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
}