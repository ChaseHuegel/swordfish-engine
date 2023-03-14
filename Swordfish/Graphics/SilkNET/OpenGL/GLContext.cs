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
        return GLThread.WaitForResult(SharderProgramArgs.Create, new SharderProgramArgs(GL, name, vertexSource, fragmentSource));
    }

    public TexImage2D CreateTexImage2D(string name, byte[] pixels, uint width, uint height, bool generateMipmaps = false)
    {
        fixed (byte* pixelPtr = pixels)
        {
            return GLThread.WaitForResult(TextureArgs.Create, new TextureArgs(GL, name, pixelPtr, width, height, generateMipmaps));
        }
    }

    public GLMaterial CreateGLMaterial(ShaderProgram shaderProgram, params TexImage2D[] texImages2D)
    {
        throw new NotImplementedException();
    }

    internal GLRenderTarget CreateGLRenderTarget(Transform transform, float[] vertexData, uint[] indices, params Material[] materials)
    {
        throw new NotImplementedException();
    }

    internal VertexArrayObject32 CreateVertexArrayObject(float[] vertexData, uint[] indices)
    {
        throw new NotImplementedException();
    }
}
