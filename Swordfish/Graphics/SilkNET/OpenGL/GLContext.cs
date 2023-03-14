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

    public ShaderProgram CreateShaderProgram(string name, string vertexSource, string fragmentSource)
    {
        return GLThread.WaitForResult(SharderProgramArgs.Factory, new SharderProgramArgs(GL, name, vertexSource, fragmentSource));
    }

    public TexImage2D CreateTexImage2D(string name, byte[] pixels, uint width, uint height, bool generateMipmaps = false)
    {
        fixed (byte* pixelPtr = pixels)
        {
            return GLThread.WaitForResult(TextureArgs.Factory, new TextureArgs(GL, name, pixelPtr, width, height, generateMipmaps));
        }
    }

    public GLMaterial CreateGLMaterial(ShaderProgram shaderProgram, params TexImage2D[] texImages2D)
    {
        return GLThread.WaitForResult(GLMaterialArgs.Factory, new GLMaterialArgs(shaderProgram, texImages2D));
    }

    internal GLRenderTarget CreateGLRenderTarget(Transform transform, float[] vertexData, uint[] indices, params GLMaterial[] materials)
    {
        return GLThread.WaitForResult(GLRenderTargetArgs.Factory, new GLRenderTargetArgs(GL, transform, vertexData, indices, materials));
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
}