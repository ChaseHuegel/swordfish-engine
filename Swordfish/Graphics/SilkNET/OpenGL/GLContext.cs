using System.Numerics;
using Silk.NET.OpenGL;
using Swordfish.Library.Extensions;
using Swordfish.Library.Types;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal unsafe partial class GLContext
{
    private readonly GL GL;
    private readonly SynchronizationContext GLThread;

    public GLContext(GL gl, SynchronizationContext synchronizationContext)
    {
        GL = gl;
        GLThread = synchronizationContext;
    }

    public ShaderComponent CreateShaderComponent(string name, Silk.NET.OpenGL.ShaderType type, string source)
    {
        return GLThread.WaitForResult(SharderComponentArgs.Factory, new SharderComponentArgs(GL, name, type, source));
    }

    public ShaderProgram CreateShaderProgram(string name, ShaderComponent[] shaderComponents)
    {
        return GLThread.WaitForResult(SharderProgramArgs.Factory, new SharderProgramArgs(GL, name, shaderComponents));
    }

    public TexImage2D CreateTexImage2D(string name, byte[] pixels, uint width, uint height, bool generateMipmaps = false)
    {
        fixed (byte* pixelPtr = pixels)
        {
            return GLThread.WaitForResult(TextureArgs.Factory, new TextureArgs(GL, name, pixelPtr, width, height, generateMipmaps));
        }
    }

    public TexImage3D CreateTexImage3D(string name, byte[] pixels, uint width, uint height, uint depth, bool generateMipmaps = false)
    {
        fixed (byte* pixelPtr = pixels)
        {
            return GLThread.WaitForResult(TextureArrayArgs.Factory, new TextureArrayArgs(GL, name, pixelPtr, width, height, depth, generateMipmaps));
        }
    }

    public GLMaterial CreateGLMaterial(ShaderProgram shaderProgram, params IGLTexture[] textures)
    {
        return GLThread.WaitForResult(GLMaterialArgs.Factory, new GLMaterialArgs(shaderProgram, textures));
    }

    internal GLRenderTarget CreateGLRenderTarget(Transform transform, VertexArrayObject<float, uint> vertexArrayObject, BufferObject<Matrix4x4> modelsBufferObject, GLMaterial[] materials, RenderOptions renderOptions)
    {
        return GLThread.WaitForResult(GLRenderTargetArgs.Factory, new GLRenderTargetArgs(GL, transform, vertexArrayObject, modelsBufferObject, materials, renderOptions));
    }

    internal VertexArrayObject<TVertexType> CreateVertexArrayObject<TVertexType>(TVertexType[] vertexData)
        where TVertexType : unmanaged
    {
        return GLThread.WaitForResult(VertexArrayObjectArgs<TVertexType>.Factory, new VertexArrayObjectArgs<TVertexType>(GL, vertexData));
    }

    internal VertexArrayObject<TVertexType, TElementType> CreateVertexArrayObject<TVertexType, TElementType>(TVertexType[] vertexData, TElementType[] indices)
        where TVertexType : unmanaged
        where TElementType : unmanaged
    {
        return GLThread.WaitForResult(VertexArrayObjectArgs<TVertexType, TElementType>.Factory, new VertexArrayObjectArgs<TVertexType, TElementType>(GL, vertexData, indices));
    }

    internal VertexArrayObject32 CreateVertexArrayObject32(float[] vertexData, uint[] indices)
    {
        return GLThread.WaitForResult(VertexArrayObject32Args.Factory, new VertexArrayObject32Args(GL, vertexData, indices));
    }

    internal BufferObject<TData> CreateBufferObject<TData>(TData[] data, BufferTargetARB bufferType, BufferUsageARB usage = BufferUsageARB.StaticDraw)
        where TData : unmanaged
    {
        return GLThread.WaitForResult(BufferObjectArgs<TData>.Factory, new BufferObjectArgs<TData>(GL, data, bufferType, usage));
    }
}